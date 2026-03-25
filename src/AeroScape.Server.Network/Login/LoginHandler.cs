using System.Buffers.Binary;
using System.Net.Sockets;
using AeroScape.Server.Core.Constants;
using AeroScape.Server.Core.Crypto;
using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
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
/// Handles the RS2 login protocol:
///   1. Exchange seeds (server → client: 8 junk bytes + 8-byte server seed)
///   2. Read login block (type, encrypted block with credentials + ISAAC seed)
///   3. Validate credentials (auto-register if new)
///   4. Build ISAAC ciphers, register player in world, create session
///   5. Send login response code
///
/// Extracted from ConnectionPipeline to keep login logic isolated and testable.
/// The pipeline calls <see cref="HandleAsync"/> and receives a fully formed
/// <see cref="LoginResult"/> (or null on failure) — no raw socket work leaks out.
/// </summary>
public sealed class LoginHandler
{
    private readonly PlayerSessionManager _sessionManager;
    private readonly GameWorld _world;
    private readonly IPlayerRepository _playerRepo;
    private readonly ItemDefinitionService _itemDefs;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        PlayerSessionManager sessionManager,
        GameWorld world,
        IPlayerRepository playerRepo,
        ItemDefinitionService itemDefs,
        ILogger<LoginHandler> logger)
    {
        _sessionManager = sessionManager;
        _world = world;
        _playerRepo = playerRepo;
        _itemDefs = itemDefs;
        _logger = logger;
    }

    /// <summary>
    /// Runs the full login handshake on a raw socket.
    /// Returns a <see cref="LoginResult"/> on success, or null if login was rejected.
    /// </summary>
    public async Task<LoginResult?> HandleAsync(Socket socket, CancellationToken ct)
    {
        var buffer = new byte[512];

        // --- Step 1: Send server seed ---
        var response = new byte[17];
        response[0] = 0; // exchange byte
        var serverSeed = Random.Shared.NextInt64();
        BinaryPrimitives.WriteInt64BigEndian(response.AsSpan(9), serverSeed);
        await socket.SendAsync(response, SocketFlags.None, ct);

        // --- Step 2: Read login type + block size ---
        int read = await socket.ReceiveAsync(buffer.AsMemory(0, 2), SocketFlags.None, ct);
        if (read < 2) return null;

        int loginType = buffer[0];
        int loginSize = buffer[1];

        // Read full login block
        int totalRead = 0;
        while (totalRead < loginSize)
        {
            read = await socket.ReceiveAsync(buffer.AsMemory(totalRead, loginSize - totalRead), SocketFlags.None, ct);
            if (read == 0) return null;
            totalRead += read;
        }

        // --- Step 3: Parse the login block ---
        var reader = new PacketReader(buffer.AsSpan(0, loginSize));

        int magicByte = reader.ReadByte();
        int clientVersion = reader.ReadShort();
        int lowMemory = reader.ReadByte();

        // Skip CRC keys (9 ints)
        for (int i = 0; i < 9; i++)
            reader.ReadInt();

        int encSize = reader.ReadByte();
        int rsaMagic = reader.ReadByte(); // should be 10

        int clientSeedHi = reader.ReadInt();
        int clientSeedLo = reader.ReadInt();
        long reportedServerSeed = reader.ReadLong();

        int uid = reader.ReadInt();
        string username = reader.ReadString().Trim().ToLowerInvariant();
        string password = reader.ReadString().Trim();

        _logger.LogInformation("Login attempt: {Username} (type={Type}, rev={Rev})",
            username, loginType, clientVersion);

        // --- Step 4: Build ISAAC ciphers ---
        int[] isaacSeed =
        [
            clientSeedHi, clientSeedLo,
            (int)(serverSeed >> 32), (int)serverSeed
        ];

        var incomingIsaac = new IsaacRandom(isaacSeed);
        var outgoingIsaac = new IsaacRandom(isaacSeed.Select(s => s + 50).ToArray());

        // --- Step 5: Validate credentials ---
        int responseCode = await ValidateCredentialsAsync(username, password, ct);

        if (responseCode != ServerConstants.LoginSuccess)
        {
            await socket.SendAsync(new byte[] { (byte)responseCode }, SocketFlags.None, ct);
            return null;
        }

        // --- Step 6: Load / create player ---
        var player = await _playerRepo.LoadAsync(username, ct) ?? new Player
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
            await socket.SendAsync(new byte[] { (byte)ServerConstants.LoginWorldFull }, SocketFlags.None, ct);
            return null;
        }

        // --- Step 8: Send login response ---
        var loginResponse = new byte[3];
        loginResponse[0] = (byte)ServerConstants.LoginSuccess;
        loginResponse[1] = (byte)player.Rights;
        loginResponse[2] = 0; // flagged
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

    private async Task<int> ValidateCredentialsAsync(string username, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 12)
            return ServerConstants.LoginInvalidCredentials;

        if (!await _playerRepo.ExistsAsync(username, ct))
        {
            // Auto-register
            await _playerRepo.CreateAsync(username, password, ct);
            return ServerConstants.LoginSuccess;
        }

        if (!await _playerRepo.ValidateCredentialsAsync(username, password, ct))
            return ServerConstants.LoginInvalidCredentials;

        if (_world.IsOnline(username))
            return ServerConstants.LoginAlreadyOnline;

        return ServerConstants.LoginSuccess;
    }
}
