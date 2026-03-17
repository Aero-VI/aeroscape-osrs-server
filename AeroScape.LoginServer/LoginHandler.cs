using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AeroScape.LoginServer;

/// <summary>
/// OSRS-compatible login protocol handler (rev 236).
///
/// Implements the full OSRS login handshake based on RSProt rev 236 protocol:
///   Phase 1  — Service selector (handshake opcode)
///   Phase 2  — Server hello (server seed)
///   Phase 3  — Login request header (login type + payload size + revision + sub fields + RSA block)
///   Phase 4  — RSA-encrypted login block (ISAAC seeds + session + OTP + credentials)
///   Phase 5  — XTEA-encrypted remainder block (username, display, machine info, CRCs)
///   Phase 6  — Server login response
///   Phase 7  — ISAAC cipher initialisation
///   Phase 8  — Game traffic loop with REBUILD_NORMAL + keepalive
///
/// For Phase 1, any username/password is accepted (no credential validation).
/// </summary>
public sealed class LoginHandler
{
    // Expected client revision — set to 0 to skip revision check
    private const int ExpectedRevision = 0;

    // Service selector opcodes
    private const byte ServiceLogin  = 14;
    private const byte ServiceUpdate = 15;

    // Login type opcodes
    private const byte LoginTypeNew       = 16;
    private const byte LoginTypeReconnect = 18;

    // RSA block magic byte (RSProt uses 1)
    private const byte RsaMagic = 1;

    // Response codes
    private const byte ResponseSuccess    = 2;
    private const byte ResponseOutdated   = 6;
    private const byte ResponseMalformed  = 22;

    // Player rights for accepted logins (player = 0)
    private const byte PlayerRights = 0;

    // OTP authentication types
    private const byte OtpToken  = 0; // trusted computer
    private const byte OtpRemember = 1; // trusted authenticator
    private const byte OtpNone   = 2; // no MFA
    private const byte OtpForget = 3; // untrusted

    // Password authentication types
    private const byte PasswordAuth = 0;
    private const byte TokenAuth    = 2;

    // Server packet opcodes (from RSProt rev 236 GameServerProtId)
    private const byte OpcodeRebuildNormal  = 34;
    private const byte OpcodeServerTickEnd  = 14;

    // Default spawn: Lumbridge
    private const int SpawnX = 3222;
    private const int SpawnZ = 3218;
    private const int SpawnLevel = 0;

    // Game tick interval (ms)
    private const int GameTickMs = 600;

    // Static player index counter
    private static int _nextPlayerIndex = 1;

    private readonly TcpClient _client;
    private readonly string _remoteEndpoint;
    private readonly CancellationToken _ct;

    public LoginHandler(TcpClient client, string remoteEndpoint, CancellationToken ct)
    {
        _client = client;
        _remoteEndpoint = remoteEndpoint;
        _ct = ct;
    }

    public async Task HandleAsync()
    {
        await using var stream = _client.GetStream();
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        try
        {
            // ── Phase 1: Service selector ──────────────────────────────────────────
            byte serviceOpcode = await ReadByteAsync(stream);
            Console.WriteLine($"[{_remoteEndpoint}] Service opcode: {serviceOpcode}");

            if (serviceOpcode == ServiceUpdate)
            {
                Console.WriteLine($"[{_remoteEndpoint}] Update/JS5 service requested — handing off to Js5Handler.");
                var js5 = new Js5Handler(_client, _remoteEndpoint, _ct);
                await js5.HandleAsync();
                return;
            }

            if (serviceOpcode != ServiceLogin)
            {
                Console.WriteLine($"[{_remoteEndpoint}] Unknown service opcode {serviceOpcode}, closing.");
                return;
            }

            // ── Phase 2: Server hello (0x00 + 8-byte seed) ────────────────────────
            long serverSeed = GenerateServerSeed();
            Console.WriteLine($"[{_remoteEndpoint}] Sending server seed: {serverSeed}");

            byte[] serverHello = new byte[9];
            serverHello[0] = 0x00;                          // status OK
            WriteBigEndianLong(serverHello, 1, serverSeed); // 8-byte seed
            await stream.WriteAsync(serverHello, _ct);
            await stream.FlushAsync(_ct);

            // ── Phase 3: Login request header ─────────────────────────────────────
            byte loginType = await ReadByteAsync(stream);
            if (loginType != LoginTypeNew && loginType != LoginTypeReconnect)
            {
                Console.WriteLine($"[{_remoteEndpoint}] Unexpected login type {loginType}, closing.");
                return;
            }

            int payloadSize    = await ReadUShortAsync(stream);
            int clientRevision = await ReadIntAsync(stream);
            int subVersion     = await ReadIntAsync(stream);
            int serverVersion  = await ReadIntAsync(stream);
            byte clientType    = await ReadByteAsync(stream);
            byte platformType  = await ReadByteAsync(stream);
            byte extAuthType   = await ReadByteAsync(stream);

            Console.WriteLine($"[{_remoteEndpoint}] Login type={loginType} payloadSize={payloadSize} revision={clientRevision}");
            Console.WriteLine($"[{_remoteEndpoint}] subVersion={subVersion} serverVersion={serverVersion} clientType={clientType} platformType={platformType} extAuthType={extAuthType}");

            // Revision check (skip if ExpectedRevision == 0)
            if (ExpectedRevision != 0 && clientRevision != ExpectedRevision)
            {
                Console.WriteLine($"[{_remoteEndpoint}] Revision mismatch (got {clientRevision}, expected {ExpectedRevision}).");
                await SendByteAsync(stream, ResponseOutdated);
                return;
            }

            // ── Phase 4: RSA block ────────────────────────────────────────────────
            int rsaSize = await ReadUShortAsync(stream);
            Console.WriteLine($"[{_remoteEndpoint}] RSA block size from client: {rsaSize}");

            int headerConsumed = 4 + 4 + 4 + 1 + 1 + 1 + 2; // 17

            byte[] rsaBlock = await ReadBytesAsync(stream, rsaSize);
            Console.WriteLine($"[{_remoteEndpoint}] RSA block ({rsaSize} bytes): {BitConverter.ToString(rsaBlock, 0, Math.Min(32, rsaSize))}...");

            // Decrypt RSA block
            byte[] plaintext;
            try
            {
                plaintext = RsaKeys.Decrypt(rsaBlock);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_remoteEndpoint}] RSA decryption failed: {ex.Message}");
                await SendByteAsync(stream, ResponseMalformed);
                return;
            }

            Console.WriteLine($"[{_remoteEndpoint}] RSA plaintext ({plaintext.Length} bytes): {BitConverter.ToString(plaintext, 0, Math.Min(40, plaintext.Length))}");

            // Parse plaintext RSA block
            int pos = 0;

            // Strip leading zero byte that BigInteger.ToByteArray can produce
            if (plaintext.Length > 0 && plaintext[0] == 0x00)
                pos = 1;

            // RSA magic check
            if (pos >= plaintext.Length || plaintext[pos] != RsaMagic)
            {
                Console.WriteLine($"[{_remoteEndpoint}] Invalid RSA magic byte: 0x{(pos < plaintext.Length ? plaintext[pos] : 0xFF):X2} (expected 0x01). RSA key mismatch?");
                await SendByteAsync(stream, ResponseMalformed);
                return;
            }
            pos++; // skip magic byte
            Console.WriteLine($"[{_remoteEndpoint}] RSA magic OK (0x01)");

            // 4 x i32 ISAAC seeds
            if (pos + 16 > plaintext.Length)
            {
                Console.WriteLine($"[{_remoteEndpoint}] RSA block too short for ISAAC seeds.");
                await SendByteAsync(stream, ResponseMalformed);
                return;
            }

            int[] isaacSeeds = new int[4];
            for (int i = 0; i < 4; i++)
            {
                isaacSeeds[i] = ReadBigEndianInt(plaintext, pos);
                pos += 4;
            }
            Console.WriteLine($"[{_remoteEndpoint}] ISAAC seeds: [{isaacSeeds[0]:X8}, {isaacSeeds[1]:X8}, {isaacSeeds[2]:X8}, {isaacSeeds[3]:X8}]");

            // Session ID (8 bytes / long)
            if (pos + 8 > plaintext.Length)
            {
                Console.WriteLine($"[{_remoteEndpoint}] RSA block too short for session ID.");
                await SendByteAsync(stream, ResponseMalformed);
                return;
            }
            long sessionId = ReadBigEndianLong(plaintext, pos);
            pos += 8;
            Console.WriteLine($"[{_remoteEndpoint}] sessionId={sessionId}");

            // OTP authentication
            if (pos >= plaintext.Length)
            {
                Console.WriteLine($"[{_remoteEndpoint}] RSA block too short for OTP type.");
                await SendByteAsync(stream, ResponseMalformed);
                return;
            }

            byte otpType = plaintext[pos++];
            Console.WriteLine($"[{_remoteEndpoint}] OTP type: {otpType}");

            switch (otpType)
            {
                case OtpToken:
                case OtpRemember:
                case OtpNone:
                case OtpForget:
                default:
                    if (pos + 4 <= plaintext.Length) pos += 4;
                    break;
            }

            // Password authentication type
            if (pos >= plaintext.Length)
            {
                Console.WriteLine($"[{_remoteEndpoint}] RSA block too short for auth type.");
                await SendByteAsync(stream, ResponseMalformed);
                return;
            }

            byte authType = plaintext[pos++];
            Console.WriteLine($"[{_remoteEndpoint}] Auth type: {authType} ({(authType == PasswordAuth ? "password" : authType == TokenAuth ? "token" : "unknown")})");

            // Password or token (null-terminated ASCII string)
            string password = ReadNullTerminatedString(plaintext, ref pos);
            Console.WriteLine($"[{_remoteEndpoint}] RSA block parsed — credential length: {password.Length}");

            // ── Phase 5: XTEA-encrypted remainder ─────────────────────────────────
            int xteaLen = payloadSize - headerConsumed - rsaSize;
            Console.WriteLine($"[{_remoteEndpoint}] XTEA block: payloadSize={payloadSize} headerConsumed={headerConsumed} rsaSize={rsaSize} xteaLen={xteaLen}");

            string username = "unknown";

            if (xteaLen > 0)
            {
                byte[] xteaBytes = await ReadBytesAsync(stream, xteaLen);
                Console.WriteLine($"[{_remoteEndpoint}] XTEA raw first bytes: {BitConverter.ToString(xteaBytes, 0, Math.Min(20, xteaLen))}");

                try
                {
                    Xtea.Decrypt(xteaBytes, 0, xteaBytes.Length, isaacSeeds);

                    int xp = 0;
                    username = ReadNullTerminatedString(xteaBytes, ref xp);
                    Console.WriteLine($"[{_remoteEndpoint}] Username: '{username}'");

                    if (xp < xteaBytes.Length)
                    {
                        byte packedSettings = xteaBytes[xp++];
                        bool lowDetail = (packedSettings & 0x1) != 0;
                        bool resizable = (packedSettings & 0x2) != 0;
                        Console.WriteLine($"[{_remoteEndpoint}] Settings: lowDetail={lowDetail} resizable={resizable}");
                    }

                    if (xp + 4 <= xteaBytes.Length)
                    {
                        ushort canvasW = (ushort)((xteaBytes[xp] << 8) | xteaBytes[xp + 1]); xp += 2;
                        ushort canvasH = (ushort)((xteaBytes[xp] << 8) | xteaBytes[xp + 1]); xp += 2;
                        Console.WriteLine($"[{_remoteEndpoint}] Canvas: {canvasW}x{canvasH}");
                    }

                    Console.WriteLine($"[{_remoteEndpoint}] XTEA block parsed (remaining {xteaBytes.Length - xp} bytes skipped)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{_remoteEndpoint}] XTEA decryption warning: {ex.Message}");
                }
            }

            // ── Phase 6: Authentication ────────────────────────────────────────────
            int playerIndex = System.Threading.Interlocked.Increment(ref _nextPlayerIndex) % 2047;
            if (playerIndex == 0) playerIndex = 1;

            Console.WriteLine($"[{_remoteEndpoint}] Accepting login for '{username}' at slot {playerIndex}");

            byte[] response = new byte[36];
            int wPos = 0;
            response[wPos++] = ResponseSuccess;        // opcode = 2
            response[wPos++] = 37;                     // payload size (VAR_BYTE)
            response[wPos++] = 0;                      // authenticator type: 0 = no authenticator
            response[wPos++] = 0;                      // authenticator code byte 1
            response[wPos++] = 0;                      // authenticator code byte 2
            response[wPos++] = 0;                      // authenticator code byte 3
            response[wPos++] = 0;                      // authenticator code byte 4
            response[wPos++] = PlayerRights;            // staffModLevel
            response[wPos++] = (PlayerRights >= 2) ? (byte)1 : (byte)0; // playerMod boolean
            response[wPos++] = (byte)(playerIndex >> 8);  // index high byte
            response[wPos++] = (byte)(playerIndex & 0xFF);// index low byte
            response[wPos++] = 1;                      // member = true
            // accountHash (8 bytes of zeros)
            for (int i = 0; i < 8; i++) response[wPos++] = 0;
            // userId (8 bytes of zeros)
            for (int i = 0; i < 8; i++) response[wPos++] = 0;
            // userHash (8 bytes of zeros)
            for (int i = 0; i < 8; i++) response[wPos++] = 0;

            await stream.WriteAsync(response, _ct);
            await stream.FlushAsync(_ct);

            Console.WriteLine($"[{_remoteEndpoint}] Sent rev 236 success response ({response.Length} bytes) for '{username}'");

            // ── Phase 7: ISAAC cipher initialisation ───────────────────────────────
            var inCipher  = new IsaacCipher(isaacSeeds);
            var outCipher = new IsaacCipher(new int[]
            {
                isaacSeeds[0] + 50,
                isaacSeeds[1] + 50,
                isaacSeeds[2] + 50,
                isaacSeeds[3] + 50
            });

            Console.WriteLine($"[{_remoteEndpoint}] ISAAC ciphers initialised for '{username}' (slot {playerIndex})");

            // ── Phase 8: Hand off to game traffic loop ─────────────────────────────
            await GameTrafficLoopAsync(stream, inCipher, outCipher, username, playerIndex);
        }
        catch (OperationCanceledException)
        {
            // Server shutting down
        }
        catch (Exception ex) when (ex is IOException || ex is SocketException)
        {
            Console.WriteLine($"[{_remoteEndpoint}] Connection error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_remoteEndpoint}] Unexpected error: {ex}");
        }
    }

    /// <summary>
    /// Post-login game traffic loop.
    /// Sends REBUILD_NORMAL to place the player in the world, then maintains
    /// the connection with periodic SERVER_TICK_END keepalives.
    /// </summary>
    private async Task GameTrafficLoopAsync(
        NetworkStream stream,
        IsaacCipher inCipher,
        IsaacCipher outCipher,
        string username,
        int playerIndex)
    {
        Console.WriteLine($"[{username}:{playerIndex}] Entering game traffic loop.");

        // ── Send REBUILD_NORMAL for Lumbridge ──────────────────────────────────
        await SendRebuildNormal(stream, outCipher, SpawnX, SpawnZ, SpawnLevel);
        Console.WriteLine($"[{username}:{playerIndex}] Sent REBUILD_NORMAL (Lumbridge {SpawnX},{SpawnZ}).");

        // ── Game loop: read client packets + send keepalives ───────────────────
        byte[] buf = new byte[4096];
        var lastTickSend = DateTime.UtcNow;

        while (!_ct.IsCancellationRequested)
        {
            // Read with a timeout so we can interleave keepalive sends
            using (var readCts = CancellationTokenSource.CreateLinkedTokenSource(_ct))
            {
                readCts.CancelAfter(GameTickMs);
                try
                {
                    int bytesRead = await stream.ReadAsync(buf.AsMemory(), readCts.Token);
                    if (bytesRead == 0)
                    {
                        Console.WriteLine($"[{username}:{playerIndex}] Client closed connection.");
                        break;
                    }

                    // Decode the opcode using the incoming ISAAC cipher
                    int maskedOpcode = buf[0] & 0xFF;
                    int rawOpcode    = (maskedOpcode - (inCipher.NextInt() & 0xFF)) & 0xFF;
                    Console.WriteLine($"[{username}:{playerIndex}] Recv packet: masked=0x{maskedOpcode:X2} raw=0x{rawOpcode:X2} ({bytesRead} bytes)");
                }
                catch (OperationCanceledException) when (!_ct.IsCancellationRequested)
                {
                    // Read timed out — normal, just send keepalive below
                }
            }

            // Send SERVER_TICK_END keepalive every game tick
            var now = DateTime.UtcNow;
            if ((now - lastTickSend).TotalMilliseconds >= GameTickMs)
            {
                try
                {
                    await SendFixedPacket(stream, outCipher, OpcodeServerTickEnd);
                    await stream.FlushAsync(_ct);
                    lastTickSend = now;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{username}:{playerIndex}] Write error: {ex.Message}");
                    break;
                }
            }
        }

        Console.WriteLine($"[{username}:{playerIndex}] Disconnected.");
    }

    // ── Outgoing Packet Helpers ────────────────────────────────────────────────

    /// <summary>
    /// Sends a REBUILD_NORMAL packet (opcode 34, VAR_SHORT).
    /// Format (from RSProt rev 236 RebuildNormalEncoder):
    ///   p2(zoneZ)  p2Alt3(zoneX)  p2Alt1(worldArea)  p2(keyCount)  [keys...]
    /// XTEA keys are all zeros (fine for F2P/unencrypted map regions).
    /// </summary>
    private static async Task SendRebuildNormal(
        NetworkStream stream, IsaacCipher outCipher,
        int absX, int absZ, int level)
    {
        int zoneX = absX >> 3;  // 3222 >> 3 = 402
        int zoneZ = absZ >> 3;  // 3218 >> 3 = 402

        // Calculate which map squares (regions) fall in the build area
        // Build area = 13x13 zones centered on player zone
        int minMsX = (zoneX - 6) >> 3;  // map square X
        int maxMsX = (zoneX + 6) >> 3;
        int minMsZ = (zoneZ - 6) >> 3;
        int maxMsZ = (zoneZ + 6) >> 3;

        int keyCount = (maxMsX - minMsX + 1) * (maxMsZ - minMsZ + 1);

        // Build payload
        // 2 (zoneZ) + 2 (zoneX) + 2 (worldArea) + 2 (keyCount) + keyCount * 16
        int payloadLen = 2 + 2 + 2 + 2 + (keyCount * 16);
        byte[] payload = new byte[payloadLen];
        int p = 0;

        // p2(zoneZ) — big-endian short
        payload[p++] = (byte)(zoneZ >> 8);
        payload[p++] = (byte)(zoneZ & 0xFF);

        // p2Alt3(zoneX) — (value+128) & 0xFF, then value >> 8
        payload[p++] = (byte)((zoneX + 128) & 0xFF);
        payload[p++] = (byte)((zoneX >> 8) & 0xFF);

        // p2Alt1(worldArea) — little-endian short (worldArea = 0)
        payload[p++] = 0;
        payload[p++] = 0;

        // p2(keyCount) — big-endian short
        payload[p++] = (byte)(keyCount >> 8);
        payload[p++] = (byte)(keyCount & 0xFF);

        // XTEA keys: all zeros (keyCount x 4 ints x 4 bytes = keyCount * 16)
        // payload is already zero-initialized, so nothing to write

        // Encode the full packet: [encrypted opcode][size:2][payload]
        byte[] packet = new byte[1 + 2 + payloadLen];
        int encOpcode = (OpcodeRebuildNormal + (outCipher.NextInt() & 0xFF)) & 0xFF;
        packet[0] = (byte)encOpcode;
        // VAR_SHORT: 2-byte big-endian length
        packet[1] = (byte)(payloadLen >> 8);
        packet[2] = (byte)(payloadLen & 0xFF);
        Buffer.BlockCopy(payload, 0, packet, 3, payloadLen);

        await stream.WriteAsync(packet);
        await stream.FlushAsync();
    }

    /// <summary>
    /// Sends a fixed-size zero-payload packet (e.g. SERVER_TICK_END).
    /// Just writes the ISAAC-encrypted opcode byte.
    /// </summary>
    private static async Task SendFixedPacket(
        NetworkStream stream, IsaacCipher outCipher, byte opcode)
    {
        int encOpcode = (opcode + (outCipher.NextInt() & 0xFF)) & 0xFF;
        await stream.WriteAsync(new byte[] { (byte)encOpcode });
    }

    // ── Helper Methods ─────────────────────────────────────────────────────────

    private static long GenerateServerSeed()
    {
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        byte[] buf = new byte[8];
        rng.GetBytes(buf);
        return ReadBigEndianLong(buf, 0);
    }

    private static async Task<byte> ReadByteAsync(NetworkStream stream)
    {
        byte[] buf = new byte[1];
        int read = await stream.ReadAsync(buf);
        if (read == 0) throw new EndOfStreamException();
        return buf[0];
    }

    private static async Task<int> ReadUShortAsync(NetworkStream stream)
    {
        byte[] buf = await ReadExactAsync(stream, 2);
        return (buf[0] << 8) | buf[1];
    }

    private static async Task<int> ReadIntAsync(NetworkStream stream)
    {
        byte[] buf = await ReadExactAsync(stream, 4);
        return ReadBigEndianInt(buf, 0);
    }

    private static async Task<byte[]> ReadBytesAsync(NetworkStream stream, int count) =>
        await ReadExactAsync(stream, count);

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count)
    {
        byte[] buf = new byte[count];
        int total = 0;
        while (total < count)
        {
            int read = await stream.ReadAsync(buf.AsMemory(total, count - total));
            if (read == 0) throw new EndOfStreamException($"Expected {count} bytes, got {total}.");
            total += read;
        }
        return buf;
    }

    private static Task SendByteAsync(NetworkStream stream, byte value) =>
        stream.WriteAsync(new[] { value }).AsTask();

    private static int ReadBigEndianInt(byte[] buf, int offset) =>
        (buf[offset]     << 24) |
        (buf[offset + 1] << 16) |
        (buf[offset + 2] <<  8) |
         buf[offset + 3];

    private static long ReadBigEndianLong(byte[] buf, int offset) =>
        ((long)buf[offset]     << 56) | ((long)buf[offset + 1] << 48) |
        ((long)buf[offset + 2] << 40) | ((long)buf[offset + 3] << 32) |
        ((long)buf[offset + 4] << 24) | ((long)buf[offset + 5] << 16) |
        ((long)buf[offset + 6] <<  8) | (long)buf[offset + 7];

    private static void WriteBigEndianLong(byte[] buf, int offset, long value)
    {
        buf[offset]     = (byte)(value >> 56);
        buf[offset + 1] = (byte)(value >> 48);
        buf[offset + 2] = (byte)(value >> 40);
        buf[offset + 3] = (byte)(value >> 32);
        buf[offset + 4] = (byte)(value >> 24);
        buf[offset + 5] = (byte)(value >> 16);
        buf[offset + 6] = (byte)(value >>  8);
        buf[offset + 7] = (byte)(value);
    }

    private static string ReadNullTerminatedString(byte[] buf, ref int pos)
    {
        int start = pos;
        while (pos < buf.Length && buf[pos] != 0x00 && buf[pos] != 0x0A)
            pos++;
        string s = Encoding.ASCII.GetString(buf, start, pos - start);
        if (pos < buf.Length) pos++; // consume terminator
        return s.ToLowerInvariant();
    }
}
