using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AeroScape.LoginServer;

/// <summary>
/// JS5 (cache update) service handler — real cache serving.
///
/// Wire protocol:
///   Handshake (client → server):  [opcode=15 (already consumed)][revision: i32 big-endian] (4 bytes)
///   Handshake response (server):   0x00 = OK
///
///   After OK, client sends requests: [priority(1)][index(1)][archive(2)] = 4 bytes each
///   Server responds per request:
///     [index(1)][archive_hi(1)][archive_lo(1)] + container_bytes with 0xFF separators every 512 bytes
///
/// Cache structure (OSRS disk store):
///   - main_file_cache.dat2   : sector data (each sector = 520 bytes)
///   - main_file_cache.idxN   : 6 bytes per archive → [size:3][sector:3]
///   - main_file_cache.idx255 : meta-index; each entry points to an index-header container
///
/// For request (index=255, archive=N): serve the index-N header container from dat2 via idx255[N]
/// For request (index=N, archive=M):   serve archive M from index N via idxN[M]
/// </summary>
public sealed class Js5Handler
{
    private const string CachePath = "/home/cache/rev236/cache";
    private const string Dat2File  = CachePath + "/main_file_cache.dat2";

    private readonly TcpClient _client;
    private readonly string    _remoteEndpoint;
    private readonly CancellationToken _ct;

    public Js5Handler(TcpClient client, string remoteEndpoint, CancellationToken ct)
    {
        _client         = client;
        _remoteEndpoint = remoteEndpoint;
        _ct             = ct;
    }

    public async Task HandleAsync()
    {
        await using var stream = _client.GetStream();

        try
        {
            // Read remaining 4 handshake bytes: revision as big-endian i32
            byte[] revBytes = await ReadExactAsync(stream, 4);
            int revision = (revBytes[0] << 24) | (revBytes[1] << 16) | (revBytes[2] << 8) | revBytes[3];
            Console.WriteLine($"[{_remoteEndpoint}] JS5 handshake — client revision: {revision}");

            // Always accept — respond 0x00 (OK)
            await stream.WriteAsync(new byte[] { 0x00 }, _ct);
            await stream.FlushAsync(_ct);
            Console.WriteLine($"[{_remoteEndpoint}] JS5 handshake accepted.");

            // Process cache requests indefinitely until client disconnects
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

                // Load the raw container bytes from cache
                byte[]? container = LoadContainer(index, archive);

                if (container == null || container.Length == 0)
                {
                    Console.WriteLine($"[{_remoteEndpoint}] JS5 WARNING: no data for index={index} archive={archive}, sending empty container");
                    container = Array.Empty<byte>();
                }
                else
                {
                    Console.WriteLine($"[{_remoteEndpoint}] JS5 serving index={index} archive={archive} size={container.Length}");
                }

                // Build and send the JS5 response
                byte[] response = BuildResponse(index, archive, container);
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
    /// Load the raw container bytes for the given (index, archive) request.
    ///
    /// OSRS cache disk format:
    ///   Each sector = 520 bytes: [archiveId:2][chunk:2][nextSector:3][indexId:1][data:512]
    ///   idxN[archive * 6 .. archive*6+5] = [size:3][startSector:3]
    ///
    /// For index=255, archive=N: read from idx255 → dat2 (index header container)
    /// For index=N,   archive=M: read from idxN   → dat2 (archive data container)
    /// </summary>
    private static byte[]? LoadContainer(int index, int archive)
    {
        string idxFile = index == 255
            ? $"{CachePath}/main_file_cache.idx255"
            : $"{CachePath}/main_file_cache.idx{index}";

        if (!File.Exists(idxFile))
        {
            Console.WriteLine($"[JS5] Index file not found: {idxFile}");
            return null;
        }

        // Read the 6-byte index entry
        byte[] entry = new byte[6];
        using (var idx = new FileStream(idxFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            long offset = (long)archive * 6;
            if (offset + 6 > idx.Length)
            {
                Console.WriteLine($"[JS5] Archive {archive} out of range in {idxFile}");
                return null;
            }
            idx.Seek(offset, SeekOrigin.Begin);
            int bytesRead = idx.Read(entry, 0, 6);
            if (bytesRead < 6)
                return null;
        }

        int size        = (entry[0] << 16) | (entry[1] << 8) | entry[2];
        int startSector = (entry[3] << 16) | (entry[4] << 8) | entry[5];

        if (size == 0 || startSector == 0)
        {
            Console.WriteLine($"[JS5] Archive {archive} in index {index} has no data (size={size}, sector={startSector})");
            return null;
        }

        if (!File.Exists(Dat2File))
        {
            Console.WriteLine($"[JS5] dat2 not found: {Dat2File}");
            return null;
        }

        // Read sector chain from dat2
        byte[] container = new byte[size];
        int containerPos = 0;
        int currentSector = startSector;
        int expectedChunk = 0;

        using var dat2 = new FileStream(Dat2File, FileMode.Open, FileAccess.Read, FileShare.Read);

        while (containerPos < size)
        {
            long sectorOffset = (long)currentSector * 520;
            if (sectorOffset + 520 > dat2.Length)
            {
                Console.WriteLine($"[JS5] Sector {currentSector} out of range in dat2");
                break;
            }

            dat2.Seek(sectorOffset, SeekOrigin.Begin);

            // Read 8-byte sector header
            byte[] header = new byte[8];
            int hr = dat2.Read(header, 0, 8);
            if (hr < 8)
                break;

            int nextSector = (header[4] << 16) | (header[5] << 8) | header[6];

            // Read up to 512 bytes of sector data
            int bytesToRead = Math.Min(512, size - containerPos);
            int bytesRead = dat2.Read(container, containerPos, bytesToRead);
            if (bytesRead <= 0)
                break;

            containerPos += bytesRead;
            currentSector = nextSector;
            expectedChunk++;
        }

        return container;
    }

    /// <summary>
    /// Build the JS5 response packet.
    ///
    /// Format:
    ///   [index:1][archive>>8:1][archive&0xFF:1]
    ///   + container bytes, with 0xFF separator byte inserted before every 512th byte
    ///     (i.e. at output positions 512, 1024, 1536... measured from start of container data)
    ///
    /// The 0xFF separator allows the client to detect chunk boundaries in the stream.
    /// </summary>
    private static byte[] BuildResponse(int index, int archive, byte[] container)
    {
        // Calculate total size: 3-byte header + container + separators
        int separators = container.Length / 512; // one separator after every full 512-byte block
        int totalSize = 3 + container.Length + separators;
        byte[] response = new byte[totalSize];
        int outPos = 0;

        // 3-byte header
        response[outPos++] = (byte)index;
        response[outPos++] = (byte)(archive >> 8);
        response[outPos++] = (byte)(archive & 0xFF);

        // Container bytes with 0xFF separators
        int containerPos = 0;
        while (containerPos < container.Length)
        {
            // Insert 0xFF separator before every 512-byte boundary (except first block)
            if (containerPos > 0 && containerPos % 512 == 0)
            {
                response[outPos++] = 0xFF;
            }

            int chunk = Math.Min(512 - (containerPos % 512), container.Length - containerPos);
            Array.Copy(container, containerPos, response, outPos, chunk);
            outPos += chunk;
            containerPos += chunk;
        }

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
