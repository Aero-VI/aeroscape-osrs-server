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
///   Server responds per request with JS5-framed container data.
///
/// JS5 Response framing (per OpenRS2 Js5ResponseEncoder):
///   Header:  [index:1][archive_hi:1][archive_lo:1][compression|prefetch_bit:1]
///   Block 0: [up to 508 bytes of container data (AFTER compression byte)]
///   Block N: [0xFF:1][up to 511 bytes of container data]
///
///   The compression byte is extracted from the container, OR'd with 0x80 if
///   the request was prefetch (priority=1), then written as the 4th header byte.
///   This gives 4 + 508 = 512 bytes for the first wire block, then 1 + 511 = 512
///   for subsequent blocks, with 0xFF separators between blocks.
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

    // Cached master checksum table - built lazily on first request
    private static byte[]? _masterChecksumTable;
    private static readonly object _masterLock = new object();

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
            // Read JS5 handshake: [revision:4][xteaKey0:4][xteaKey1:4][xteaKey2:4][xteaKey3:4] = 20 bytes
            // OSRS/RuneLite clients send the full 20-byte handshake after the opcode byte.
            // The first 4 bytes are the client build/revision.
            // The next 16 bytes are four 32-bit XTEA keys (typically all zeros).
            // If we only read 4, the remaining 16 key bytes get misinterpreted as JS5 requests.
            byte[] handshake = await ReadExactAsync(stream, 20);
            int revision = (handshake[0] << 24) | (handshake[1] << 16) | (handshake[2] << 8) | handshake[3];
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

                byte opcode = requestBuf[0];

                // Handle JS5 state notifications (non-cache-request message types)
                // Type 0/1 = cache request, Type 2/3 = login state, Type 4 = encryption keys
                if (opcode == 2 || opcode == 3)
                {
                    string state = opcode == 2 ? "logged out" : "logged in";
                    Console.WriteLine($"[{_remoteEndpoint}] JS5 state notification — {state} (no response)");
                    continue;
                }

                if (opcode == 4)
                {
                    // Encryption key notification: 3 bytes already in requestBuf[1..3],
                    // read 12 more bytes for the remaining XTEA key data
                    byte[] xteaRemaining = await ReadExactAsync(stream, 12);
                    Console.WriteLine($"[{_remoteEndpoint}] JS5 encryption keys received (no response)");
                    continue;
                }

                if (opcode != 0 && opcode != 1)
                {
                    Console.WriteLine($"[{_remoteEndpoint}] JS5 unknown opcode {opcode}, skipping");
                    continue;
                }

                bool prefetch = opcode == 1;
                byte index    = requestBuf[1];
                int  archive  = (requestBuf[2] << 8) | requestBuf[3];

                Console.WriteLine($"[{_remoteEndpoint}] JS5 request — prefetch={prefetch} index={index} archive={archive}");

                byte[]? container;

                // Special case: index=255, archive=255 = master checksum table
                if (index == 255 && archive == 255)
                {
                    container = GetOrBuildMasterChecksumTable();
                    Console.WriteLine($"[{_remoteEndpoint}] JS5 serving master checksum table: {container?.Length ?? 0} bytes");
                }
                else
                {
                    container = LoadContainer(index, archive);
                }

                if (container == null || container.Length == 0)
                {
                    Console.WriteLine($"[{_remoteEndpoint}] JS5 WARNING: no data for index={index} archive={archive}, skipping");
                    continue;
                }

                // Strip 2-byte version trailer from regular archive containers.
                // Per OpenRS2-667 Js5Service: VersionTrailer.strip() is called when
                // archive != ARCHIVESET (255). Index headers (archive=255) and the
                // master index (255,255) do NOT have version trailers.
                if (index != 255)
                {
                    container = StripVersionTrailer(container, index, archive);
                }

                if (!(index == 255 && archive == 255))
                {
                    Console.WriteLine($"[{_remoteEndpoint}] JS5 serving index={index} archive={archive} size={container.Length}");
                }

                // For master checksum table (255,255), NEVER set prefetch bit — it corrupts the compression byte
                bool usePrefetch = (index == 255 && archive == 255) ? false : prefetch;
                byte[] response = BuildResponse(index, archive, container, usePrefetch);
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
    /// Load the pre-built master checksum table from master_index.dat.
    /// </summary>
    private static byte[]? GetOrBuildMasterChecksumTable()
    {
        if (_masterChecksumTable != null)
            return _masterChecksumTable;
        lock (_masterLock)
        {
            if (_masterChecksumTable != null)
                return _masterChecksumTable;
            string path = CachePath + "/master_index.dat";
            if (!File.Exists(path))
            {
                Console.WriteLine("[JS5] master_index.dat not found");
                return null;
            }
            _masterChecksumTable = File.ReadAllBytes(path);
            Console.WriteLine($"[JS5] Loaded master_index.dat: {_masterChecksumTable.Length} bytes");
            return _masterChecksumTable;
        }
    }

    /// <summary>
    /// Load the container bytes for the given (index, archive) request.
    ///
    /// OSRS cache disk format:
    ///   Each sector = 520 bytes: [archiveId:2][chunk:2][nextSector:3][indexId:1][data:512]
    ///   idxN[archive * 6 .. archive*6+5] = [size:3][startSector:3]
    ///
    /// For index=255, archive=N: read from idx255 → dat2 (index header container)
    /// For index=N,   archive=M: read from idxN   → dat2 (archive data container)
    ///
    /// IMPORTANT: The disk store appends a 2-byte version trailer to each container.
    /// The JS5 wire protocol sends ONLY the container data (compression header + payload),
    /// NOT the version trailer. We must strip it by computing the true container size
    /// from the compression header.
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
    /// Wire format (per OpenRS2 Js5ResponseEncoder / Js5ResponseDecoder):
    ///
    ///   The compression byte (container[0]) is extracted from the container data,
    ///   OR'd with 0x80 if the request was prefetch, and written as a 4th header byte.
    ///
    ///   Header:   [index:1][archive_hi:1][archive_lo:1][compression_byte:1] = 4 bytes
    ///   Block 0:  [up to 508 bytes of remaining container data]
    ///   Block N:  [0xFF:1][up to 511 bytes of remaining container data]
    ///
    ///   First wire segment: 4 + 508 = 512 bytes → then 0xFF
    ///   Subsequent:         1 + 511 = 512 bytes → then 0xFF
    ///   (Last block may be shorter, no trailing 0xFF)
    /// </summary>
    private static byte[] BuildResponse(int index, int archive, byte[] container, bool prefetch)
    {
        if (container.Length < 1)
            return new byte[] { (byte)index, (byte)(archive >> 8), (byte)(archive & 0xFF), 0x00 };

        // Extract the compression type byte from the container and set prefetch bit
        byte compressionByte = container[0];
        if (prefetch)
        {
            compressionByte |= 0x80;
        }

        // Remaining container data (everything after the compression byte)
        int dataLen = container.Length - 1;

        // Calculate number of 0xFF separators needed
        // First block: 4 header bytes + 508 data bytes = 512 wire bytes
        // Subsequent blocks: 1 (0xFF) + 511 data bytes = 512 wire bytes
        int separators;
        if (dataLen <= 508)
        {
            separators = 0;
        }
        else
        {
            separators = 1 + (dataLen - 509) / 511;
        }

        int totalSize = 4 + dataLen + separators;
        byte[] response = new byte[totalSize];
        int outPos = 0;

        // Write 4-byte header
        response[outPos++] = (byte)index;
        response[outPos++] = (byte)(archive >> 8);
        response[outPos++] = (byte)(archive & 0xFF);
        response[outPos++] = compressionByte;

        int containerPos = 1; // skip compression byte (already written to header)

        // First block: up to 508 bytes of container data
        int firstChunk = Math.Min(508, dataLen);
        if (firstChunk > 0)
        {
            Array.Copy(container, containerPos, response, outPos, firstChunk);
            outPos += firstChunk;
            containerPos += firstChunk;
        }

        // Subsequent blocks: 0xFF separator + up to 511 bytes of container data
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

    /// <summary>
    /// Strip the 2-byte version trailer from a container read from the disk store.
    /// The disk store appends [version:2] after the container data. The JS5 wire
    /// protocol must not include this trailer.
    ///
    /// Container format:
    ///   type=0 (none):  [type:1][len:4][data:len]                     = 5 + len
    ///   type=1 (bzip2): [type:1][compLen:4][decompLen:4][data:compLen] = 9 + compLen
    ///   type=2 (gzip):  [type:1][compLen:4][decompLen:4][data:compLen] = 9 + compLen
    /// </summary>
    private static byte[] StripVersionTrailer(byte[] container, int index, int archive)
    {
        if (container.Length < 5)
            return container;

        int compType = container[0];
        int compLen = (container[1] << 24) | (container[2] << 16) | (container[3] << 8) | container[4];
        int trueSize = compType == 0 ? (5 + compLen) : (9 + compLen);

        if (trueSize > 0 && trueSize < container.Length)
        {
            int trailer = container.Length - trueSize;
            Console.WriteLine($"[JS5] Stripping {trailer}-byte version trailer from idx{index}[{archive}]: {container.Length} → {trueSize}");
            byte[] trimmed = new byte[trueSize];
            Array.Copy(container, trimmed, trueSize);
            return trimmed;
        }

        return container;
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
