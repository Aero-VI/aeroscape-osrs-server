using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AeroScape.LoginServer;

/// <summary>
/// JS5 (cache update) service handler.
///
/// Wire protocol:
///   Handshake (client → server):  [opcode=15][revision_hi][revision_lo]  (3 bytes)
///   Handshake response (server):   0x00 = OK, 0x06 = OUTDATED
///
///   After OK, client sends requests: [priority][index][archive]  (3 bytes each)
///   Server responds with cache data per request.
///
/// This implementation is a STUB that:
///   - Accepts the handshake for any revision (returns 0x00).
///   - Responds to every cache request with a minimal valid empty container
///     so the client passes the JS5 gate and can proceed to login.
/// </summary>
public sealed class Js5Handler
{
    // Opcode written by LoginHandler before handing off here (already consumed)
    // Remaining handshake: [revision_hi][revision_lo] — 2 more bytes to read.

    private readonly TcpClient _client;
    private readonly string _remoteEndpoint;
    private readonly CancellationToken _ct;

    public Js5Handler(TcpClient client, string remoteEndpoint, CancellationToken ct)
    {
        _client = client;
        _remoteEndpoint = remoteEndpoint;
        _ct = ct;
    }

    public async Task HandleAsync()
    {
        await using var stream = _client.GetStream();

        try
        {
            // Read remaining 4 handshake bytes: revision as big-endian i32
            // OSRS protocol: [opcode=15 (already consumed by LoginHandler)][revision: i32 (4 bytes)]
            byte[] revBytes = await ReadExactAsync(stream, 4);
            int revision = (revBytes[0] << 24) | (revBytes[1] << 16) | (revBytes[2] << 8) | revBytes[3];
            Console.WriteLine($"[{_remoteEndpoint}] JS5 handshake — client revision: {revision}");

            // Always accept — respond 0x00 (OK)
            await stream.WriteAsync(new byte[] { 0x00 }, _ct);
            await stream.FlushAsync(_ct);
            Console.WriteLine($"[{_remoteEndpoint}] JS5 handshake accepted.");

            // Process cache requests indefinitely until client disconnects
            // OSRS JS5 request format: [priority (1)][index (1)][archive (2 bytes, big-endian)] = 4 bytes
            byte[] requestBuf = new byte[4];
            while (!_ct.IsCancellationRequested)
            {
                int totalRead = 0;
                while (totalRead < 4)
                {
                    int r = await stream.ReadAsync(requestBuf.AsMemory(totalRead, 4 - totalRead), _ct);
                    if (r == 0)
                    {
                        Console.WriteLine($"[{_remoteEndpoint}] JS5 client disconnected.");
                        return;
                    }
                    totalRead += r;
                }

                byte priority = requestBuf[0];
                byte index    = requestBuf[1];
                int  archive  = (requestBuf[2] << 8) | requestBuf[3];

                Console.WriteLine($"[{_remoteEndpoint}] JS5 request — priority={priority} index={index} archive={archive}");

                // Respond with a minimal valid cache container.
                // OSRS JS5 response format:
                //   [index (1)][archive_hi (1)][archive_lo (1)][compression (1)][length: i32 (4)][data...]
                // Total header = 8 bytes. For an empty container: compression=0, length=0, no data.
                byte[] response = BuildEmptyContainerResponse(index, archive);
                await stream.WriteAsync(response, _ct);
                await stream.FlushAsync(_ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Server shutting down — normal
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine($"[{_remoteEndpoint}] JS5 client disconnected (EOF).");
        }
        catch (Exception ex) when (ex is IOException || ex is SocketException)
        {
            Console.WriteLine($"[{_remoteEndpoint}] JS5 connection error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_remoteEndpoint}] JS5 unexpected error: {ex}");
        }
    }

    /// <summary>
    /// Build a minimal JS5 response for an empty cache container.
    ///
    /// JS5 response per request (OSRS protocol):
    ///   Byte 0       : index
    ///   Byte 1-2     : archive (big-endian short for main index, just byte for others —
    ///                  most clients treat it as 2 bytes; we send 2)
    ///   Byte 3       : compression type (0 = none)
    ///   Bytes 4-7    : uncompressed length (i32 big-endian) = 0
    ///   (no further bytes for length-0 payload)
    ///
    /// Note: OSRS splits the payload into 512-byte chunks with 0xFF bytes between them,
    /// but an empty payload means no chunks — so no separator is needed.
    /// </summary>
    private static byte[] BuildEmptyContainerResponse(byte index, int archive)
    {
        // 7-byte header: [index(1)][archive_hi(1)][archive_lo(1)][compression(1)][length(4)]
        byte[] buf = new byte[7];
        buf[0] = index;
        buf[1] = 0;          // archive hi byte
        buf[2] = archive;    // archive lo byte
        buf[3] = 0;          // compression = none
        buf[4] = 0;
        buf[5] = 0;
        buf[6] = 0;
        // length = 0, so bytes 4-7 are all zero (buf[4..6] already zero; we need one more byte)
        // Oops — need 4 bytes for length. Let's make it 8 bytes total.
        byte[] response = new byte[8];
        response[0] = index;
        response[1] = (byte)(archive >> 8);   // archive hi
        response[2] = (byte)(archive & 0xFF); // archive lo
        response[3] = 0;       // compression = none
        response[4] = 0;       // length byte 0 (MSB)
        response[5] = 0;       // length byte 1
        response[6] = 0;       // length byte 2
        response[7] = 0;       // length byte 3 (LSB) — length = 0
        return response;
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count)
    {
        byte[] buf = new byte[count];
        int total = 0;
        while (total < count)
        {
            int r = await stream.ReadAsync(buf.AsMemory(total, count - total));
            if (r == 0) throw new EndOfStreamException($"Expected {count} bytes, got {total}.");
            total += r;
        }
        return buf;
    }
}
