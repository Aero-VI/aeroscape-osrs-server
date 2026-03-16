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
/// For request (index=255, archive=N):   serve the index-N header container from dat2 via idx255[N]
/// For request (index=255, archive=255): serve the dynamically-built master checksum table
/// For request (index=N, archive=M):     serve archive M from index N via idxN[M]
/// </summary>
public sealed class Js5Handler
{
    private const string CachePath = "/home/cache/rev236/cache";
    private const string Dat2File  = CachePath + "/main_file_cache.dat2";

    // --- AEROSCAPE START ---
    // Cached master checksum table - built lazily on first request
    private static byte[]? _masterChecksumTable;
    private static readonly object _masterLock = new object();
    // --- AEROSCAPE END ---

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

                byte[]? container;

                // --- AEROSCAPE START ---
                // Special case: index=255, archive=255 = master checksum table
                // This is not stored in the disk cache but must be computed dynamically
                // from the CRC32 + version of each sub-index's reference container.
                if (index == 255 && archive == 255)
                {
                    container = GetOrBuildMasterChecksumTable();
                    Console.WriteLine($"[{_remoteEndpoint}] JS5 serving master checksum table: {container?.Length ?? 0} bytes");
                }
                else
                {
                    container = LoadContainer(index, archive);
                }
                // --- AEROSCAPE END ---

                if (container == null || container.Length == 0)
                {
                    Console.WriteLine($"[{_remoteEndpoint}] JS5 WARNING: no data for index={index} archive={archive}, sending empty container");
                    container = Array.Empty<byte>();
                }
                else if (!(index == 255 && archive == 255))
                {
                    Console.WriteLine($"[{_remoteEndpoint}] JS5 serving index={index} archive={archive} size={container.Length}");
                }

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

    // --- AEROSCAPE START ---
    /// <summary>
    /// Build or return the cached master checksum table for index=255, archive=255.
    ///
    /// The OSRS master checksum table is NOT stored in idx255 as entry 255.
    /// It is dynamically constructed from CRC32 checksums and version numbers
    /// of each sub-index's reference container (index=255, archive=0 to numIndices-1).
    ///
    /// Output container format (uncompressed, compression=0):
    ///   [compression=0 (1 byte)]
    ///   [uncompressed_length (4 bytes big-endian)]
    ///   For each sub-index i (0 to numIndices-1):
    ///     [crc32_of_raw_container (4 bytes big-endian)]
    ///     [version (4 bytes big-endian)]
    /// </summary>
    private static byte[]? GetOrBuildMasterChecksumTable()
    {
        if (_masterChecksumTable != null)
            return _masterChecksumTable;

        lock (_masterLock)
        {
            if (_masterChecksumTable != null)
                return _masterChecksumTable;

            try
            {
                string idx255Path = $"{CachePath}/main_file_cache.idx255";
                if (!File.Exists(idx255Path))
                {
                    Console.WriteLine("[JS5] idx255 not found, cannot build master checksum table");
                    return null;
                }

                long idx255Length = new FileInfo(idx255Path).Length;
                int numIndices = (int)(idx255Length / 6); // 6 bytes per entry in idx255
                Console.WriteLine($"[JS5] Building master checksum table for {numIndices} sub-indices...");

                // Each index entry: [crc32 (4)] + [version (4)] = 8 bytes
                byte[] table = new byte[numIndices * 8];

                for (int i = 0; i < numIndices; i++)
                {
                    byte[]? container = LoadContainer(255, i);
                    if (container == null || container.Length == 0)
                    {
                        Console.WriteLine($"[JS5] Master table: sub-index {i} empty, using crc=0 version=0");
                        continue;
                    }

                    // CRC32 of the raw container bytes EXCLUDING the last 2 bytes (version trailer)
                    uint crc = ComputeCrc32(container[0..^2]);

                    // Version: the last 2 bytes of the container as a big-endian unsigned short.
                    // OSRS containers have a 2-byte version/revision trailer at the end.
                    int version = 0;
                    if (container.Length >= 2)
                    {
                        version = (container[^2] << 8) | container[^1];
                    }

                    int off = i * 8;
                    table[off]     = (byte)(crc >> 24);
                    table[off + 1] = (byte)(crc >> 16);
                    table[off + 2] = (byte)(crc >> 8);
                    table[off + 3] = (byte)crc;
                    table[off + 4] = (byte)(version >> 24);
                    table[off + 5] = (byte)(version >> 16);
                    table[off + 6] = (byte)(version >> 8);
                    table[off + 7] = (byte)version;
                }

                // Wrap table in an uncompressed OSRS container:
                // [compression=0][length_4_BE][table_bytes]
                byte[] result = new byte[5 + table.Length];
                result[0] = 0; // compression = none
                result[1] = (byte)(table.Length >> 24);
                result[2] = (byte)(table.Length >> 16);
                result[3] = (byte)(table.Length >> 8);
                result[4] = (byte)table.Length;
                Array.Copy(table, 0, result, 5, table.Length);

                _masterChecksumTable = result;
                Console.WriteLine($"[JS5] Master checksum table built: {result.Length} bytes ({numIndices} entries)");
                return _masterChecksumTable;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JS5] Error building master checksum table: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// CRC32 using the standard OSRS/ZIP polynomial 0xEDB88320.
    /// </summary>
    private static uint ComputeCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return crc ^ 0xFFFFFFFF;
    }
    // --- AEROSCAPE END ---

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

        byte[] container = new byte[size];
        int containerPos = 0;
        int currentSector = startSector;

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

            byte[] header = new byte[8];
            int hr = dat2.Read(header, 0, 8);
            if (hr < 8)
                break;

            int nextSector = (header[4] << 16) | (header[5] << 8) | header[6];

            int bytesToRead = Math.Min(512, size - containerPos);
            int bytesRead = dat2.Read(container, containerPos, bytesToRead);
            if (bytesRead <= 0)
                break;

            containerPos += bytesRead;
            currentSector = nextSector;
        }

        return container;
    }

    /// <summary>
    /// Build the JS5 response packet.
    ///
    /// Format (OSRS JS5 framing — 512-byte OUTPUT STREAM blocks):
    ///   Block 0: [index:1][arch_hi:1][arch_lo:1][data: up to 509 bytes] = 512 bytes
    ///   Block N (N>0): [0xFF:1][data: up to 511 bytes] = 512 bytes
    ///
    /// Separators are at OUTPUT boundaries, not container-data boundaries.
    /// </summary>
    private static byte[] BuildResponse(int index, int archive, byte[] container)
    {
        // OSRS JS5 framing: 512-byte output blocks
        // Block 0: [index:1][arch_hi:1][arch_lo:1][data:up to 509 bytes]
        // Block N (N>0): [0xFF:1][data:up to 511 bytes]
        int separators = container.Length <= 509 ? 0 : 1 + (container.Length - 510) / 511;
        int totalSize = 3 + container.Length + separators;
        byte[] response = new byte[totalSize];
        int outPos = 0;

        response[outPos++] = (byte)index;
        response[outPos++] = (byte)(archive >> 8);
        response[outPos++] = (byte)(archive & 0xFF);

        int containerPos = 0;

        // First block: up to 509 container bytes (no separator)
        int firstChunk = Math.Min(509, container.Length);
        Array.Copy(container, containerPos, response, outPos, firstChunk);
        outPos += firstChunk;
        containerPos += firstChunk;

        // Subsequent blocks: 0xFF separator + up to 511 container bytes
        while (containerPos < container.Length)
        {
            response[outPos++] = 0xFF;
            int chunk = Math.Min(511, container.Length - containerPos);
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
