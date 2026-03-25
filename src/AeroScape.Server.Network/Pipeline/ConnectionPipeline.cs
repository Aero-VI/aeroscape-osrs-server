using System.Buffers.Binary;
using System.Net.Sockets;
using AeroScape.Server.Core.Constants;
using AeroScape.Server.Core.Crypto;
using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Pipeline;

/// <summary>
/// Handles the full lifecycle of a client connection:
/// 1. Handshake (service request)
/// 2. Login sequence (credentials + ISAAC seed)
/// 3. Game packet loop (decode → dispatch → handler)
/// </summary>
public sealed class ConnectionPipeline
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PlayerSessionManager _sessionManager;
    private readonly GameWorld _world;
    private readonly ProtocolService _protocol;
    private readonly IPlayerRepository _playerRepo;
    private readonly ILogger<ConnectionPipeline> _logger;

    public ConnectionPipeline(
        IServiceProvider serviceProvider,
        PlayerSessionManager sessionManager,
        GameWorld world,
        ProtocolService protocol,
        IPlayerRepository playerRepo,
        ILogger<ConnectionPipeline> logger)
    {
        _serviceProvider = serviceProvider;
        _sessionManager = sessionManager;
        _world = world;
        _protocol = protocol;
        _playerRepo = playerRepo;
        _logger = logger;
    }

    public async Task ProcessAsync(Socket socket, CancellationToken ct)
    {
        var buffer = new byte[512];

        // Step 1: Read service request byte
        int read = await socket.ReceiveAsync(buffer.AsMemory(0, 1), SocketFlags.None, ct);
        if (read == 0) return;

        int serviceRequest = buffer[0];

        switch (serviceRequest)
        {
            case 14: // Login request
                await HandleLoginAsync(socket, buffer, ct);
                break;
            case 15: // JS5 / Update request
                await HandleUpdateRequestAsync(socket, buffer, ct);
                break;
            default:
                _logger.LogWarning("Unknown service request: {Request}", serviceRequest);
                break;
        }
    }

    private async Task HandleUpdateRequestAsync(Socket socket, byte[] buffer, CancellationToken ct)
    {
        // JS5 / cache update protocol — stub for now
        // Read version
        int read = await socket.ReceiveAsync(buffer.AsMemory(0, 4), SocketFlags.None, ct);
        if (read < 4) return;

        int clientVersion = BinaryPrimitives.ReadInt32BigEndian(buffer);
        
        if (clientVersion != ServerConstants.Revision)
        {
            await socket.SendAsync(new byte[] { 6 }, SocketFlags.None, ct); // outdated
            return;
        }

        await socket.SendAsync(new byte[] { 0 }, SocketFlags.None, ct); // ok

        // TODO: Implement JS5 cache serving pipeline
        _logger.LogDebug("JS5 request received — stub response sent");
    }

    private async Task HandleLoginAsync(Socket socket, byte[] buffer, CancellationToken ct)
    {
        // Send 8 ignored bytes + server seed (long)
        var response = new byte[17];
        response[0] = 0; // status
        var serverSeed = Random.Shared.NextInt64();
        BinaryPrimitives.WriteInt64BigEndian(response.AsSpan(9), serverSeed);
        await socket.SendAsync(response, SocketFlags.None, ct);

        // Read login type + block size
        int read = await socket.ReceiveAsync(buffer.AsMemory(0, 2), SocketFlags.None, ct);
        if (read < 2) return;

        int loginType = buffer[0]; // 16 = normal, 18 = reconnect
        int loginSize = buffer[1];

        // Read full login block
        int totalRead = 0;
        while (totalRead < loginSize)
        {
            read = await socket.ReceiveAsync(buffer.AsMemory(totalRead, loginSize - totalRead), SocketFlags.None, ct);
            if (read == 0) return;
            totalRead += read;
        }

        var reader = new PacketReader(buffer.AsSpan(0, loginSize));

        int magicByte = reader.ReadByte(); // should be 255
        int clientVersion = reader.ReadShort();
        int lowMemory = reader.ReadByte();

        // Skip CRC keys (9 ints = 36 bytes)
        for (int i = 0; i < 9; i++)
            reader.ReadInt();

        // Encrypted block length
        int encSize = reader.ReadByte(); // should be loginSize - position - 1... or RSA block

        // RSA block (unencrypted for private servers)
        int rsaMagic = reader.ReadByte(); // should be 10
        
        // ISAAC seed
        int clientSeedHi = reader.ReadInt();
        int clientSeedLo = reader.ReadInt();
        long reportedServerSeed = reader.ReadLong();

        int uid = reader.ReadInt();
        string username = reader.ReadString().Trim().ToLowerInvariant();
        string password = reader.ReadString().Trim();

        _logger.LogInformation("Login attempt: {Username} (type={Type}, rev={Rev})", 
            username, loginType, clientVersion);

        // Build ISAAC ciphers
        int[] isaacSeed =
        [
            clientSeedHi, clientSeedLo,
            (int)(serverSeed >> 32), (int)serverSeed
        ];
        
        var incomingIsaac = new IsaacRandom(isaacSeed);
        var outgoingIsaac = new IsaacRandom(isaacSeed.Select(s => s + 50).ToArray());

        // Validate / auto-register
        int responseCode;
        if (!await _playerRepo.ExistsAsync(username, ct))
        {
            await _playerRepo.CreateAsync(username, password, ct);
            responseCode = ServerConstants.LoginSuccess;
        }
        else if (!await _playerRepo.ValidateCredentialsAsync(username, password, ct))
        {
            responseCode = ServerConstants.LoginInvalidCredentials;
        }
        else if (_world.IsOnline(username))
        {
            responseCode = ServerConstants.LoginAlreadyOnline;
        }
        else
        {
            responseCode = ServerConstants.LoginSuccess;
        }

        if (responseCode != ServerConstants.LoginSuccess)
        {
            await socket.SendAsync(new byte[] { (byte)responseCode }, SocketFlags.None, ct);
            return;
        }

        // Load player data
        var player = await _playerRepo.LoadAsync(username, ct) ?? new Player
        {
            Username = username,
            Password = password,
            Position = Position.Default
        };

        int index = _world.Register(player);
        if (index == -1)
        {
            await socket.SendAsync(new byte[] { (byte)ServerConstants.LoginWorldFull }, SocketFlags.None, ct);
            return;
        }

        // Send login success response
        var loginResponse = new byte[3];
        loginResponse[0] = (byte)ServerConstants.LoginSuccess;
        loginResponse[1] = (byte)player.Rights;
        loginResponse[2] = 0; // flagged
        await socket.SendAsync(loginResponse, SocketFlags.None, ct);

        // Create session and enter game loop
        var session = new PlayerSession(_sessionManager.NextSessionId(), socket, player)
        {
            IncomingCipher = incomingIsaac,
            OutgoingCipher = outgoingIsaac,
            SessionManager = _sessionManager
        };
        _sessionManager.Register(session);

        try
        {
            // Send initial data
            await SendInitialDataAsync(session, ct);

            // Enter packet processing loop
            await ProcessGamePacketsAsync(session, ct);
        }
        finally
        {
            // Cleanup
            await _playerRepo.SaveAsync(player, ct);
            _world.Unregister(player);
            _sessionManager.Unregister(session);
            session.Dispose();
            _logger.LogInformation("Player {Username} disconnected", username);
        }
    }

    private async Task SendInitialDataAsync(PlayerSession session, CancellationToken ct)
    {
        var player = session.Player;

        // Send map region
        await PacketSender.SendMapRegion(session, _protocol, ct);
        player.NeedsMapRegionUpdate = false;
        player.LastKnownRegion = player.Position;

        // Send sidebar interfaces (typical 508 sidebar config)
        int[] sidebarInterfaces = [
            2423,  // Attack (tab 0)
            3917,  // Skills (tab 1)
            638,   // Quest (tab 2)
            3213,  // Inventory (tab 3)
            1644,  // Equipment (tab 4)
            5608,  // Prayer (tab 5)
            12855, // Magic (tab 6)
            -1,    // Unused (tab 7)
            5065,  // Friends (tab 8)
            5715,  // Ignore (tab 9)
            2449,  // Logout (tab 10)
            904,   // Settings (tab 11)
            147,   // Emotes (tab 12)
            -1     // Music (tab 13)
        ];

        for (int i = 0; i < sidebarInterfaces.Length; i++)
        {
            if (sidebarInterfaces[i] == -1) continue;
            await PacketSender.SendSidebar(session, _protocol, i, sidebarInterfaces[i], ct);
        }

        // Send skills
        await PacketSender.SendAllSkills(session, _protocol, ct);

        // Send inventory and equipment
        await PacketSender.SendInventory(session, _protocol, ct);
        await PacketSender.SendEquipment(session, _protocol, ct);

        // Send run energy
        await PacketSender.SendEnergy(session, _protocol, ct);

        // Send weight
        await PacketSender.SendWeight(session, _protocol, ct);

        // Set player options (right-click)
        await PacketSender.SendPlayerOption(session, _protocol, "Follow", 1, false, ct);
        await PacketSender.SendPlayerOption(session, _protocol, "Trade with", 2, false, ct);
        await PacketSender.SendPlayerOption(session, _protocol, "Req Assist", 3, false, ct);

        // Send welcome message
        await PacketSender.SendMessage(session, _protocol, "Welcome to AeroScape.", ct);
        await PacketSender.SendMessage(session, _protocol, $"There are currently {_world.PlayerCount} player(s) online.", ct);
    }

    private async Task ProcessGamePacketsAsync(PlayerSession session, CancellationToken ct)
    {
        var buffer = new byte[5000];
        
        while (session.IsConnected && !ct.IsCancellationRequested)
        {
            int read;
            try
            {
                read = session.Player.Index > 0
                    ? await ReceiveWithTimeoutAsync(session, buffer, ct)
                    : 0;
            }
            catch
            {
                break;
            }

            if (read == 0) break;

            int offset = 0;
            while (offset < read)
            {
                // Decode opcode (ISAAC encrypted)
                int rawOpcode = buffer[offset++] & 0xFF;
                int opcode = session.IncomingCipher != null
                    ? (rawOpcode - session.IncomingCipher.NextInt()) & 0xFF
                    : rawOpcode;

                // Look up packet size from protocol
                var pktDef = _protocol.GetIncoming(opcode);
                int size = pktDef?.Size ?? 0;

                if (size == -1) // var byte
                {
                    if (offset >= read) break;
                    size = buffer[offset++] & 0xFF;
                }
                else if (size == -2) // var short
                {
                    if (offset + 1 >= read) break;
                    size = BinaryPrimitives.ReadInt16BigEndian(buffer.AsSpan(offset)) & 0xFFFF;
                    offset += 2;
                }

                if (offset + size > read) break;

                var payload = buffer.AsSpan(offset, size);
                offset += size;

                // Dispatch to handler
                if (pktDef != null)
                {
                    await DispatchPacketAsync(session, pktDef, payload.ToArray(), ct);
                }
            }
        }
    }

    private static async Task<int> ReceiveWithTimeoutAsync(PlayerSession session, byte[] buffer, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30)); // 30s idle timeout
        
        try
        {
            return await session.Socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (SocketException)
        {
            return 0;
        }
    }

    private async Task DispatchPacketAsync(PlayerSession session, PacketDefinition pktDef, byte[] payload, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<PacketDispatcher>();
        await dispatcher.DispatchAsync(session, pktDef, payload, ct);
    }
}
