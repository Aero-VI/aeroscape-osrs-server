using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AeroScape.LoginServer;

/// <summary>
/// OSRS-compatible login protocol handler.
///
/// Implements the full OSRS login handshake:
///   Phase 1  — Service selector (handshake opcode)
///   Phase 2  — Server hello (server seed)
///   Phase 3  — Login request header
///   Phase 4  — RSA-encrypted login block (credentials + ISAAC seeds)
///   Phase 5  — XTEA-encrypted remainder block (client metadata)
///   Phase 6  — Server login response
///   Phase 7  — ISAAC cipher initialisation
///
/// For Phase 1, any username/password is accepted (no credential validation).
/// </summary>
public sealed class LoginHandler
{
    // Expected client revision — set to 0 to skip revision check in Phase 1
    private const int ExpectedRevision = 0;

    // Service selector opcodes
    private const byte ServiceLogin  = 14;
    private const byte ServiceUpdate = 15;

    // Login type opcodes
    private const byte LoginTypeNew       = 16;
    private const byte LoginTypeReconnect = 18;

    // RSA block magic byte
    private const byte RsaMagic = 0x0A;

    // Response codes
    private const byte ResponseSuccess    = 2;
    private const byte ResponseOutdated   = 6;
    private const byte ResponseMalformed  = 22;

    // Player rights for accepted logins (player = 0)
    private const byte PlayerRights = 0;

    // Static player index counter — a real server would use a pool; this is fine for Phase 1
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
                Console.WriteLine($"[{_remoteEndpoint}] Update/JS5 service requested — not implemented.");
                await SendByteAsync(stream, 0);
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

            int payloadSize     = await ReadUShortAsync(stream);
            int clientRevision  = await ReadIntAsync(stream);
            int subVersion      = await ReadIntAsync(stream);
            byte clientType     = await ReadByteAsync(stream);
            int rsaBlockSize    = await ReadUShortAsync(stream);

            Console.WriteLine($"[{_remoteEndpoint}] Login type={loginType} revision={clientRevision} rsaLen={rsaBlockSize}");

            // Revision check (skip if ExpectedRevision == 0)
            if (ExpectedRevision != 0 && clientRevision != ExpectedRevision)
            {
                Console.WriteLine($"[{_remoteEndpoint}] Revision mismatch (got {clientRevision}, expected {ExpectedRevision}).");
                await SendByteAsync(stream, ResponseOutdated);
                return;
            }

            // ── Phase 4: RSA block ─────────────────────────────────────────────────
            if (rsaBlockSize <= 0 || rsaBlockSize > 512)
            {
                Console.WriteLine($"[{_remoteEndpoint}] Invalid RSA block size {rsaBlockSize}.");
                await SendByteAsync(stream, ResponseMalformed);
                return;
            }

            byte[] rsaBytes = await ReadBytesAsync(stream, rsaBlockSize);
            byte[] plaintext;

            try
            {
                plaintext = RsaKeys.Decrypt(rsaBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_remoteEndpoint}] RSA decryption failed: {ex.Message}");
                await SendByteAsync(stream, ResponseMalformed);
                return;
            }

            // Parse plaintext RSA block
            int pos = 0;

            // Strip leading zero byte that BigInteger.ToByteArray can produce
            if (plaintext.Length > 0 && plaintext[0] == 0x00)
                pos = 1;

            if (pos >= plaintext.Length || plaintext[pos] != RsaMagic)
            {
                Console.WriteLine($"[{_remoteEndpoint}] RSA magic byte mismatch (got {(pos < plaintext.Length ? plaintext[pos] : -1):X2}).");
                await SendByteAsync(stream, ResponseMalformed);
                return;
            }
            pos++;

            // 4 × i32 ISAAC seeds
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

            // UID (i64) — store for logging/anti-cheat
            if (pos + 8 > plaintext.Length)
            {
                Console.WriteLine($"[{_remoteEndpoint}] RSA block too short for UID.");
                await SendByteAsync(stream, ResponseMalformed);
                return;
            }
            long uid = ReadBigEndianLong(plaintext, pos);
            pos += 8;

            // Username (null-terminated ASCII)
            string username = ReadNullTerminatedString(plaintext, ref pos);
            // Password (null-terminated ASCII)
            string password = ReadNullTerminatedString(plaintext, ref pos);

            Console.WriteLine($"[{_remoteEndpoint}] Login attempt — user: '{username}' uid: {uid}");

            // ── Phase 5: XTEA-encrypted remainder ─────────────────────────────────
            // Calculate remaining bytes: payloadSize covers everything after the u16 payload-size field.
            // Bytes consumed from payload: 4 (revision) + 4 (sub-version) + 1 (client type)
            //                              + 2 (rsa block size) + rsaBlockSize
            int headerConsumed = 4 + 4 + 1 + 2 + rsaBlockSize;
            int xteatLen = payloadSize - headerConsumed;

            if (xteatLen > 0)
            {
                byte[] xteaBytes = await ReadBytesAsync(stream, xteatLen);
                try
                {
                    Xtea.Decrypt(xteaBytes, 0, xteaBytes.Length, isaacSeeds);
                    // Phase 1: parse display mode / canvas size if desired; currently just logged
                    if (xteaBytes.Length >= 5)
                    {
                        byte displayMode  = xteaBytes[0];
                        ushort canvasW    = (ushort)((xteaBytes[1] << 8) | xteaBytes[2]);
                        ushort canvasH    = (ushort)((xteaBytes[3] << 8) | xteaBytes[4]);
                        Console.WriteLine($"[{_remoteEndpoint}] Display mode={displayMode} canvas={canvasW}×{canvasH}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{_remoteEndpoint}] XTEA decryption warning: {ex.Message}");
                    // Non-fatal — continue with login
                }
            }

            // ── Phase 6: Authentication ────────────────────────────────────────────
            // Phase 1: Accept any username/password — no credential validation.
            int playerIndex = System.Threading.Interlocked.Increment(ref _nextPlayerIndex) % 2047;
            if (playerIndex == 0) playerIndex = 1; // index 0 is reserved

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
            // Phase 1: We simply echo packets back or wait for disconnect.
            // A full game server would pass (inCipher, outCipher, playerIndex) to the game world here.
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
    /// Reads incoming packets, unmasks opcodes with the incoming ISAAC cipher.
    /// In Phase 1, the loop simply keeps the connection alive until the client disconnects.
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

            // Unmask the opcode of the first byte of each packet
            int maskedOpcode = buf[0] & 0xFF;
            int rawOpcode    = (maskedOpcode - inCipher.NextInt()) & 0xFF;
            Console.WriteLine($"[{username}:{playerIndex}] Received packet — masked={maskedOpcode:X2} raw={rawOpcode:X2} ({bytesRead} bytes)");

            // Phase 1: no game packet processing beyond opcode unmasking
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
