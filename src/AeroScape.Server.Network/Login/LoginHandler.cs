using System.Buffers.Binary;
using System.Net.Sockets;
using AeroScape.Server.Core.Constants;
using AeroScape.Server.Core.Crypto;
using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Login;

/// <summary>
/// Result of a successful login sequence.
/// Contains everything the connection pipeline needs to start the game loop.
/// </summary>
public sealed class LoginResult
{
    public required Player Player { get; init; }
    public required PlayerSession Session { get; init; }
}

/// <summary>
/// Handles the RS2 508 login protocol:
///   1. Read name-hash byte (client sends it after opcode 14)
///   2. Send server seed response: [0x00] + [8 zero bytes] + [8-byte server seed]
///   3. Read login type (16=normal, 18=reconnect) + 2-byte block size
///   4. Read full login block: version, display info, CRC keys, encrypted credentials
///   5. Validate credentials (auto-register if new)
///   6. Build ISAAC ciphers, register player in world, create session
///   7. Send 9-byte login response
///
/// Matches legacy Java server (DavidScape Login.java) byte-for-byte.
/// </summary>
public sealed class LoginHandler
{
    private readonly PlayerSessionManager _sessionManager;
    private readonly GameWorld _world;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ItemDefinitionService _itemDefs;
    private readonly ILogger<LoginHandler> _logger;

    /// <summary>
    /// RS2 base-37 character set for long↔string username encoding.
    /// </summary>
    private static readonly char[] ValidChars =
    [
        '_', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i',
        'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's',
        't', 'u', 'v', 'w', 'x', 'y', 'z', '0', '1', '2',
        '3', '4', '5', '6', '7', '8', '9'
    ];

    public LoginHandler(
        PlayerSessionManager sessionManager,
        GameWorld world,
        IServiceScopeFactory scopeFactory,
        ItemDefinitionService itemDefs,
        ILogger<LoginHandler> logger)
    {
        _sessionManager = sessionManager;
        _world = world;
        _scopeFactory = scopeFactory;
        _itemDefs = itemDefs;
        _logger = logger;
    }

    /// <summary>
    /// Runs the full 508 login handshake on a raw socket.
    /// Returns a <see cref="LoginResult"/> on success, or null if login was rejected.
    ///
    /// The ConnectionPipeline has already consumed the service byte (14).
    /// </summary>
    public async Task<LoginResult?> HandleAsync(Socket socket, CancellationToken ct)
    {
        var buffer = new byte[512];

        // --- Step 0: Read the name-hash byte (client sends it with opcode 14) ---
        int read = await socket.ReceiveAsync(buffer.AsMemory(0, 1), SocketFlags.None, ct);
        if (read == 0) return null;
        // Name hash byte is unused but must be consumed.

        // --- Step 1: Send server seed ---
        // Format: 1 byte (0) + 8 zero bytes + 8-byte server seed = 17 bytes
        var seedResponse = new byte[17];
        seedResponse[0] = 0; // exchange byte
        var serverSeed = Random.Shared.NextInt64();
        BinaryPrimitives.WriteInt64BigEndian(seedResponse.AsSpan(9), serverSeed);
        await socket.SendAsync(seedResponse, SocketFlags.None, ct);

        // --- Step 2: Read login type (1 byte) + block size (2 bytes) ---
        read = await ReadExactAsync(socket, buffer, 0, 3, ct);
        if (read < 3) return null;

        int loginType = buffer[0];
        int loginPacketSize = (buffer[1] << 8) | buffer[2];

        if (loginType != 16 && loginType != 18)
        {
            _logger.LogWarning("Unexpected login type: {Type}", loginType);
            return null;
        }

        if (loginPacketSize <= 0 || loginPacketSize > 500)
        {
            _logger.LogWarning("Invalid login packet size: {Size}", loginPacketSize);
            return null;
        }

        // Read full login block
        int totalRead = await ReadExactAsync(socket, buffer, 0, loginPacketSize, ct);
        if (totalRead < loginPacketSize) return null;

        // --- Step 3: Parse the 508 login block ---
        var reader = new PacketReader(buffer.AsSpan(0, loginPacketSize));

        // Client version (4 bytes)
        int clientVersion = reader.ReadInt();
        if (clientVersion != ServerConstants.Revision)
        {
            _logger.LogWarning("Client version mismatch: {ClientVer} != {ServerVer}",
                clientVersion, ServerConstants.Revision);
            // Allow it through — the legacy server accepted 508, 800, and 900
        }

        // Display settings
        reader.ReadByte();   // low memory / HD flag
        reader.ReadUShort(); // screen width
        reader.ReadUShort(); // screen height
        reader.ReadUShort(); // display setting

        // Cache CRC indices (24 bytes)
        for (int i = 0; i < 24; i++)
            reader.ReadByte();

        // Junk string (settings/junk data from client)
        reader.ReadString();

        // 29 ints (junk / anti-cheat / machine info)
        for (int i = 0; i < 29; i++)
            reader.ReadInt();

        // Encrypted block marker
        // loginEncryptPacketSize is decremented, then we read the RSA/encryption byte
        int encryptionByte = reader.ReadByte();

        bool usingHD = encryptionByte == 10;

        // The encryption byte should be 10 (RSA magic) or 64
        // If it's neither, the next byte is the actual RSA magic
        if (encryptionByte != 10 && encryptionByte != 64)
        {
            encryptionByte = reader.ReadByte();
        }

        if (encryptionByte != 10 && encryptionByte != 64)
        {
            _logger.LogWarning("Invalid RSA magic byte: {Byte}", encryptionByte);
            return null;
        }

        // Client session key (8 bytes)
        long clientSessionKey = reader.ReadLong();

        // Server session key (8 bytes) — should match what we sent
        long reportedServerSeed = reader.ReadLong();

        // Username encoded as a long (RS2 base-37 encoding, 8 bytes)
        long usernameLong = reader.ReadLong();
        string username = LongToString(usernameLong).ToLowerInvariant().Replace('_', ' ').Trim();

        // Password (null-terminated string)
        string password = reader.ReadString().Trim();

        if (string.IsNullOrEmpty(username))
        {
            _logger.LogWarning("Empty username in login block");
            return null;
        }

        _logger.LogInformation("Login attempt: {Username} (type={Type}, rev={Rev}, hd={HD})",
            username, loginType, clientVersion, usingHD);

        // --- Step 4: Build ISAAC ciphers ---
        int clientSeedHi = (int)(clientSessionKey >> 32);
        int clientSeedLo = (int)clientSessionKey;
        int[] isaacSeed =
        [
            clientSeedHi, clientSeedLo,
            (int)(serverSeed >> 32), (int)serverSeed
        ];

        // Client encrypts outgoing opcodes with seed+50, so server must decrypt with seed+50.
        // Client decrypts incoming opcodes with base seed, so server must encrypt with base seed.
        var incomingIsaac = new IsaacRandom(isaacSeed.Select(s => s + 50).ToArray());
        var outgoingIsaac = new IsaacRandom(isaacSeed);

        // --- Step 5: Validate credentials (scoped — DbContext is scoped) ---
        await using var scope = _scopeFactory.CreateAsyncScope();
        var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();

        int responseCode = await ValidateCredentialsAsync(playerRepo, username, password, ct);

        if (responseCode != ServerConstants.LoginSuccess)
        {
            // Even on failure, send the full 9-byte response the client expects
            var failResponse = new byte[9];
            failResponse[0] = (byte)responseCode;
            await socket.SendAsync(failResponse, SocketFlags.None, ct);
            return null;
        }

        // --- Step 6: Load / create player ---
        var player = await playerRepo.LoadAsync(username, ct) ?? new Player
        {
            Username = username,
            Password = password,
            Position = Position.Default
        };

        player.Inventory.StackChecker = _itemDefs.IsStackable;

        // --- Step 7: Register in world ---
        int index = _world.Register(player);
        if (index == -1)
        {
            var fullResponse = new byte[9];
            fullResponse[0] = (byte)ServerConstants.LoginWorldFull;
            await socket.SendAsync(fullResponse, SocketFlags.None, ct);
            return null;
        }

        // --- Step 8: Send 9-byte login response ---
        // Matches the legacy Java server exactly:
        //   [returnCode:1][rights:1][0:1][0:1][0:1][1:1][0:1][playerId:1][0:1]
        var loginResponse = new byte[9];
        loginResponse[0] = (byte)ServerConstants.LoginSuccess;  // return code
        loginResponse[1] = (byte)player.Rights;                  // player rights
        loginResponse[2] = 0;                                    // flagged
        loginResponse[3] = 0;
        loginResponse[4] = 0;
        loginResponse[5] = 1;                                    // members flag
        loginResponse[6] = 0;
        loginResponse[7] = (byte)index;                          // player index
        loginResponse[8] = 0;
        await socket.SendAsync(loginResponse, SocketFlags.None, ct);

        // --- Step 9: Create session ---
        var session = new PlayerSession(_sessionManager.NextSessionId(), socket, player)
        {
            IncomingCipher = incomingIsaac,
            OutgoingCipher = outgoingIsaac,
            SessionManager = _sessionManager
        };
        _sessionManager.Register(session);

        _logger.LogInformation("Player {Username} logged in (index={Index}, rights={Rights})",
            username, index, player.Rights);

        return new LoginResult
        {
            Player = player,
            Session = session
        };
    }

    /// <summary>
    /// Reads exactly <paramref name="count"/> bytes from the socket.
    /// Returns the number of bytes actually read.
    /// </summary>
    private static async Task<int> ReadExactAsync(Socket socket, byte[] buffer, int offset, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int n = await socket.ReceiveAsync(
                buffer.AsMemory(offset + totalRead, count - totalRead),
                SocketFlags.None, ct);
            if (n == 0) return totalRead;
            totalRead += n;
        }
        return totalRead;
    }

    /// <summary>
    /// Converts an RS2 base-37 encoded long to a string.
    /// This is the standard RuneScape username encoding used in revision 508.
    /// </summary>
    private static string LongToString(long value)
    {
        if (value <= 0 || value >= 0x5B5B57F8A98A5DD1L)
            return "invalid";

        var chars = new char[12];
        int pos = 12;

        while (value != 0)
        {
            long prev = value;
            value /= 37;
            int charIndex = (int)(prev - value * 37);
            if (charIndex < ValidChars.Length)
                chars[--pos] = ValidChars[charIndex];
            else
                chars[--pos] = '?';
        }

        return new string(chars, pos, 12 - pos);
    }

    private async Task<int> ValidateCredentialsAsync(IPlayerRepository playerRepo, string username, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 12)
            return ServerConstants.LoginInvalidCredentials;

        if (!await playerRepo.ExistsAsync(username, ct))
        {
            // Auto-register
            await playerRepo.CreateAsync(username, password, ct);
            return ServerConstants.LoginSuccess;
        }

        if (!await playerRepo.ValidateCredentialsAsync(username, password, ct))
            return ServerConstants.LoginInvalidCredentials;

        if (_world.IsOnline(username))
            return ServerConstants.LoginAlreadyOnline;

        return ServerConstants.LoginSuccess;
    }
}
