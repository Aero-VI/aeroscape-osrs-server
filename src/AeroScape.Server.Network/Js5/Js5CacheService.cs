using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Js5;

/// <summary>
/// JS5 cache service for revision 508.
/// Loads cache files from disk and serves them via the JS5 protocol.
///
/// Cache format (RuneScape disk store):
///   - main_file_cache.dat2: sector data (each sector = 520 bytes)
///   - main_file_cache.idxN: 6 bytes per archive → [size:3][sector:3]
///   - main_file_cache.idx255: meta-index; each entry points to index header containers
///
/// JS5 Wire protocol:
///   Request: [priority:1][index:1][archive:2] = 4 bytes
///   Response: [index:1][archive:2][compression:1][data...] with 0xFF block separators every 512 bytes
/// </summary>
public sealed class Js5CacheService
{
    private string? _cachePath;
    private readonly ConcurrentDictionary<(int, int), byte[]?> _containerCache = new();
    private byte[]? _masterChecksumTable;
    private readonly ILogger<Js5CacheService> _logger;

    public bool IsLoaded => _cachePath != null;

    public Js5CacheService(ILogger<Js5CacheService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initializes the cache service with the path to the cache directory.
    /// The directory should contain main_file_cache.dat2 and idx files.
    /// </summary>
    public void Load(string cachePath)
    {
        if (!Directory.Exists(cachePath))
        {
            _logger.LogWarning("Cache directory not found: {Path}. JS5 serving disabled.", cachePath);
            return;
        }

        var dat2Path = Path.Combine(cachePath, "main_file_cache.dat2");
        if (!File.Exists(dat2Path))
        {
            _logger.LogWarning("main_file_cache.dat2 not found in {Path}. JS5 serving disabled.", cachePath);
            return;
        }

        _cachePath = cachePath;

        // Count available indices
        int indexCount = 0;
        for (int i = 0; i <= 255; i++)
        {
            var idxPath = Path.Combine(cachePath, $"main_file_cache.idx{i}");
            if (File.Exists(idxPath)) indexCount++;
        }

        // Build or load master checksum table
        var masterPath = Path.Combine(cachePath, "master_index.dat");
        if (File.Exists(masterPath))
        {
            _masterChecksumTable = File.ReadAllBytes(masterPath);
            _logger.LogInformation("Loaded pre-built master checksum table: {Size} bytes", _masterChecksumTable.Length);
        }
        else
        {
            _masterChecksumTable = BuildMasterChecksumTable();
            _logger.LogInformation("Built master checksum table: {Size} bytes", _masterChecksumTable?.Length ?? 0);
        }

        _logger.LogInformation("JS5 cache loaded from {Path} — {Count} indices available", cachePath, indexCount);
    }

    /// <summary>
    /// Gets the container data for the given index and archive.
    /// Returns null if not found.
    /// </summary>
    public byte[]? GetContainer(int index, int archive)
    {
        if (_cachePath == null) return null;

        // Master checksum table
        if (index == 255 && archive == 255)
            return _masterChecksumTable;

        return _containerCache.GetOrAdd((index, archive), key => LoadContainer(key.Item1, key.Item2));
    }

    /// <summary>
    /// Builds the JS5 response frame for a container.
    /// Includes 3-byte header and 0xFF block separators every 512 bytes.
    /// </summary>
    public static byte[] BuildResponse(int index, int archive, byte[] container)
    {
        int firstChunk = Math.Min(509, container.Length);
        int remaining = container.Length - firstChunk;
        int separators = remaining <= 0 ? 0 : 1 + (remaining - 1) / 511;
        int totalSize = 3 + container.Length + separators;
        byte[] response = new byte[totalSize];
        int outPos = 0;

        response[outPos++] = (byte)index;
        response[outPos++] = (byte)(archive >> 8);
        response[outPos++] = (byte)(archive & 0xFF);

        int containerPos = 0;
        int firstBlock = Math.Min(509, container.Length);
        Array.Copy(container, containerPos, response, outPos, firstBlock);
        outPos += firstBlock;
        containerPos += firstBlock;

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

    private byte[]? LoadContainer(int index, int archive)
    {
        if (_cachePath == null) return null;

        string idxFile = Path.Combine(_cachePath, index == 255
            ? "main_file_cache.idx255"
            : $"main_file_cache.idx{index}");

        if (!File.Exists(idxFile)) return null;

        byte[] entry = new byte[6];
        using (var idx = new FileStream(idxFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            long offset = (long)archive * 6;
            if (offset + 6 > idx.Length) return null;
            idx.Seek(offset, SeekOrigin.Begin);
            if (idx.Read(entry, 0, 6) < 6) return null;
        }

        int size = (entry[0] << 16) | (entry[1] << 8) | entry[2];
        int startSector = (entry[3] << 16) | (entry[4] << 8) | entry[5];

        if (size == 0 || startSector == 0) return null;

        var dat2Path = Path.Combine(_cachePath, "main_file_cache.dat2");
        byte[] container = new byte[size];
        int containerPos = 0;
        int currentSector = startSector;

        using var dat2 = new FileStream(dat2Path, FileMode.Open, FileAccess.Read, FileShare.Read);

        while (containerPos < size)
        {
            long sectorOffset = (long)currentSector * 520;
            if (sectorOffset + 520 > dat2.Length) break;

            dat2.Seek(sectorOffset, SeekOrigin.Begin);

            byte[] header = new byte[8];
            if (dat2.Read(header, 0, 8) < 8) break;

            int nextSector = (header[4] << 16) | (header[5] << 8) | header[6];
            int bytesToRead = Math.Min(512, size - containerPos);
            int bytesRead = dat2.Read(container, containerPos, bytesToRead);
            if (bytesRead <= 0) break;

            containerPos += bytesRead;
            currentSector = nextSector;
        }

        return containerPos >= size ? container : null;
    }

    private byte[]? BuildMasterChecksumTable()
    {
        if (_cachePath == null) return null;

        // Count indices
        int indexCount = 0;
        for (int i = 0; i < 255; i++)
        {
            var idxPath = Path.Combine(_cachePath, $"main_file_cache.idx{i}");
            if (File.Exists(idxPath)) indexCount = i + 1;
        }

        if (indexCount == 0) return null;

        // Build simple checksum table
        // Format: compression type 0, then [crc:4][version:4] per index
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write((byte)0); // compression type = none
        bw.Write(ToBigEndian(indexCount * 8)); // length

        for (int i = 0; i < indexCount; i++)
        {
            var container = LoadContainer(255, i);
            if (container != null)
            {
                int crc = CalculateCrc32(container);
                bw.Write(ToBigEndian(crc));
                bw.Write(ToBigEndian(0)); // version
            }
            else
            {
                bw.Write(ToBigEndian(0));
                bw.Write(ToBigEndian(0));
            }
        }

        return ms.ToArray();
    }

    private static int ToBigEndian(int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes);
    }

    private static int CalculateCrc32(byte[] data)
    {
        // Simple CRC32 implementation matching Java's CRC32
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 1) != 0)
                    crc = (crc >> 1) ^ 0xEDB88320;
                else
                    crc >>= 1;
            }
        }
        return (int)(crc ^ 0xFFFFFFFF);
    }
}
