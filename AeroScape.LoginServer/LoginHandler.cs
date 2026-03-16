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
            // Rev 236 format (from RSProt LoginBlockDecoder):
            //   loginType     (1 byte)  — already read for routing
            //   payloadSize   (2 bytes, big-endian)
            //   version       (4 bytes, g4) — client revision
            //   subVersion    (4 bytes, g4)
            //   serverVersion (4 bytes, g4) — *** WAS MISSING ***
            //   clientType    (1 byte, g1)
            //   platformType  (1 byte, g1)
            //   extAuthType   (1 byte, g1)
            //   rsaSize       (2 bytes, g2) — RSA block length prefix
            //   [rsaBlock ... rsaSize bytes]
            //   [xteaBlock ... remaining bytes]

            byte loginType = await ReadByteAsync(stream);
            if (loginType != LoginTypeNew && loginType != LoginTypeReconnect)
            {
                Console.WriteLine($"[{_remoteEndpoint}] Unexpected login type {loginType}, closing.");
                return;
            }

            int payloadSize    = await ReadUShortAsync(stream);
            int clientRevision = await ReadIntAsync(stream);
            int subVersion     = await ReadIntAsync(stream);
            int serverVersion  = await ReadIntAsync(stream);  // *** NEW: was missing ***
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
            // RSProt: val rsaSize = buffer.g2()
            int rsaSize = await ReadUShortAsync(stream);
            Console.WriteLine($"[{_remoteEndpoint}] RSA block size from client: {rsaSize}");

            // Header bytes consumed from payload: version(4) + subVersion(4) + serverVersion(4) + clientType(1) + platformType(1) + extAuthType(1) + rsaSize(2) = 17
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

            // Session ID (8 bytes / long) — from RSProt: sessionId = rsaBuffer.g8()
            if (pos + 8 > plaintext.Length)
            {
                Console.WriteLine($"[{_remoteEndpoint}] RSA block too short for session ID.");
                await SendByteAsync(stream, ResponseMalformed);
                return;
            }
            long sessionId = ReadBigEndianLong(plaintext, pos);
            pos += 8;
            Console.WriteLine($"[{_remoteEndpoint}] sessionId={sessionId}");

            // OTP authentication — from RSProt: decodeOtpAuthentication
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
                case OtpToken: // trusted computer — 4 bytes identifier
                    if (pos + 4 <= plaintext.Length) pos += 4;
                    break;
                case OtpRemember: // trusted authenticator — 3 bytes key + 1 skip
                    if (pos + 4 <= plaintext.Length) pos += 4;
                    break;
                case OtpNone: // no MFA — skip 4 bytes
                    if (pos + 4 <= plaintext.Length) pos += 4;
                    break;
                case OtpForget: // untrusted — 3 bytes key + 1 skip
                    if (pos + 4 <= plaintext.Length) pos += 4;
                    break;
                default:
                    Console.WriteLine($"[{_remoteEndpoint}] Unknown OTP type {otpType}, skipping 4 bytes.");
                    if (pos + 4 <= plaintext.Length) pos += 4;
                    break;
            }

            // Password authentication type — from RSProt: decodeAuthentication
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
            // The XTEA block is the rest of the payload after header + rsaSize prefix + RSA block
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

                    // Parse XTEA block — from RSProt:
                    //   username (null-terminated string, gjstr)
                    //   packedClientSettings (1 byte)
                    //   width (2 bytes)
                    //   height (2 bytes)
                    //   uuid (24 bytes)
                    //   siteSettings (null-terminated string)
                    //   affiliate (4 bytes)
                    //   deepLinkCount (1 byte) + deepLinks
                    //   hostPlatformStats (variable)
                    //   secondClientType (1 byte)
                    //   reflectionCheckerConst (4 bytes)
                    //   CRC values
                    int xp = 0;

                    // Username (null-terminated string)
                    username = ReadNullTerminatedString(xteaBytes, ref xp);
                    Console.WriteLine($"[{_remoteEndpoint}] Username: '{username}'");

                    // Packed client settings
                    if (xp < xteaBytes.Length)
                    {
                        byte packedSettings = xteaBytes[xp++];
                        bool lowDetail = (packedSettings & 0x1) != 0;
                        bool resizable = (packedSettings & 0x2) != 0;
                        Console.WriteLine($"[{_remoteEndpoint}] Settings: lowDetail={lowDetail} resizable={resizable}");
                    }

                    // Canvas dimensions
                    if (xp + 4 <= xteaBytes.Length)
                    {
                        ushort canvasW = (ushort)((xteaBytes[xp] << 8) | xteaBytes[xp + 1]); xp += 2;
                        ushort canvasH = (ushort)((xteaBytes[xp] << 8) | xteaBytes[xp + 1]); xp += 2;
                        Console.WriteLine($"[{_remoteEndpoint}] Canvas: {canvasW}x{canvasH}");
                    }

                    // Skip remaining XTEA data (uuid, siteSettings, machine info, CRCs, etc.)
                    Console.WriteLine($"[{_remoteEndpoint}] XTEA block parsed (remaining {xteaBytes.Length - xp} bytes skipped)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{_remoteEndpoint}] XTEA decryption warning: {ex.Message}");
                    // Non-fatal — continue with login
                }
            }

            // ── Phase 6: Authentication ────────────────────────────────────────────
            // Accept any username/password — no credential validation.
            int playerIndex = System.Threading.Interlocked.Increment(ref _nextPlayerIndex) % 2047;
            if (playerIndex == 0) playerIndex = 1;

            Console.WriteLine($"[{_remoteEndpoint}] Accepting login for '{username}' at slot {playerIndex}");

            // Success response: [2][rights][0x00][playerIndex: u16]
            byte[] response = new byte[5];
            response[0] = ResponseSuccess;
            response[1] = PlayerRights;
            response[2] = 0x00;
            response[3] = (byte)(playerIndex >> 8);
            response[4] = (byte)(playerIndex & 0xFF);
            await stream.WriteAsync(response, _ct);
            await stream.FlushAsync(_ct);

            Console.WriteLine($"[{_remoteEndpoint}] Sent success response (code 2) for '{username}'");

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
    /// Minimal post-login game traffic loop.
    /// </summary>
    private async Task GameTrafficLoopAsync(
        NetworkStream stream,
        IsaacCipher inCipher,
        IsaacCipher outCipher,
        string username,
        int playerIndex)
    {
        Console.WriteLine($"[{username}:{playerIndex}] Entering game traffic loop.");

        byte[] buf = new byte[4096];
        while (!_ct.IsCancellationRequested)
        {
            int bytesRead;
            try
            {
                bytesRead = await stream.ReadAsync(buf, _ct);
            }
            catch (Exception)
            {
                break;
            }

            if (bytesRead == 0)
                break;

            int maskedOpcode = buf[0] & 0xFF;
            int rawOpcode    = (maskedOpcode - inCipher.NextInt()) & 0xFF;
            Console.WriteLine($"[{username}:{playerIndex}] Received packet — masked={maskedOpcode:X2} raw={rawOpcode:X2} ({bytesRead} bytes)");
        }

        Console.WriteLine($"[{username}:{playerIndex}] Disconnected.");
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
