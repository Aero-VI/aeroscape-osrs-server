using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net.Sockets;
using AeroScape.Server.Core.Constants;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Game;
using AeroScape.Server.Network.Js5;
using AeroScape.Server.Network.Login;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Pipeline;

/// <summary>
/// Handles the full lifecycle of a client connection:
/// 1. Handshake (service request)
/// 2. Login sequence (delegated to <see cref="LoginHandler"/>)
/// 3. Game packet loop using System.IO.Pipelines for zero-copy framing
///
/// The pipeline uses a producer/consumer pattern:
/// - Producer: reads raw bytes from the socket into pooled pipe buffers
/// - Consumer: parses ISAAC-encrypted game packets from the pipe
///
/// Benefits over raw Socket.ReceiveAsync:
/// - Zero-copy buffering with pooled memory segments
/// - Automatic back-pressure (PauseWriterThreshold)
/// - Clean handling of partial packets across buffer boundaries
/// - ReadOnlySequence spans multiple memory segments without copying
/// </summary>
public sealed class ConnectionPipeline
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PlayerSessionManager _sessionManager;
    private readonly GameWorld _world;
    private readonly ProtocolService _protocol;
    private readonly Js5CacheService _js5Cache;
    private readonly LoginHandler _loginHandler;
    private readonly PacketRouter _packetRouter;
    private readonly ILogger<ConnectionPipeline> _logger;

    public ConnectionPipeline(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        PlayerSessionManager sessionManager,
        GameWorld world,
        ProtocolService protocol,
        Js5CacheService js5Cache,
        LoginHandler loginHandler,
        PacketRouter packetRouter,
        ILogger<ConnectionPipeline> logger)
    {
        _serviceProvider = serviceProvider;
        _scopeFactory = scopeFactory;
        _sessionManager = sessionManager;
        _world = world;
        _protocol = protocol;
        _js5Cache = js5Cache;
        _loginHandler = loginHandler;
        _packetRouter = packetRouter;
        _logger = logger;
    }

    public async Task ProcessAsync(Socket socket, CancellationToken ct)
    {
        var buffer = new byte[1];

        // Step 1: Read service request byte (handshake)
        int read = await socket.ReceiveAsync(buffer, SocketFlags.None, ct);
        if (read == 0) return;

        int serviceRequest = buffer[0];

        switch (serviceRequest)
        {
            case 14: // Login request
                await HandleLoginAsync(socket, ct);
                break;
            case 15: // JS5 / Update request
                await HandleUpdateRequestAsync(socket, ct);
                break;
            default:
                _logger.LogWarning("Unknown service request: {Request}", serviceRequest);
                break;
        }
    }

    #region JS5 Cache Serving (Pipelines)

    private async Task HandleUpdateRequestAsync(Socket socket, CancellationToken ct)
    {
        var buffer = new byte[4];
        int read = await socket.ReceiveAsync(buffer, SocketFlags.None, ct);
        if (read < 4) return;

        int clientVersion = BinaryPrimitives.ReadInt32BigEndian(buffer);

        if (clientVersion != ServerConstants.Revision)
        {
            await socket.SendAsync(new byte[] { 6 }, SocketFlags.None, ct);
            return;
        }

        await socket.SendAsync(new byte[] { 0 }, SocketFlags.None, ct);

        if (!_js5Cache.IsLoaded)
        {
            _logger.LogWarning("JS5 request but no cache loaded — closing connection");
            return;
        }

        var pipe = new Pipe(new PipeOptions(
            minimumSegmentSize: 512,
            pauseWriterThreshold: 64 * 1024,
            resumeWriterThreshold: 32 * 1024));

        var fillTask = FillPipeFromSocketAsync(socket, pipe.Writer, ct);
        var processTask = ProcessJs5PipeAsync(socket, pipe.Reader, ct);

        await Task.WhenAny(fillTask, processTask);
        pipe.Writer.Complete();
        pipe.Reader.Complete();
    }

    private async Task ProcessJs5PipeAsync(Socket socket, PipeReader reader, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(ct);
                var buffer = result.Buffer;

                while (TryParseJs5Request(ref buffer, out byte opcode, out int index, out int archive))
                {
                    if (opcode == 2 || opcode == 3)
                        continue;

                    if (opcode == 4)
                    {
                        if (buffer.Length < 12) break;
                        buffer = buffer.Slice(12);
                        continue;
                    }

                    if (opcode != 0 && opcode != 1) continue;

                    var container = _js5Cache.GetContainer(index, archive);
                    if (container != null && container.Length > 0)
                    {
                        var response = Js5CacheService.BuildResponse(index, archive, container);
                        await socket.SendAsync(response, SocketFlags.None, ct);
                    }
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted) break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogTrace(ex, "JS5 pipe ended"); }
        finally { await reader.CompleteAsync(); }
    }

    private static bool TryParseJs5Request(ref ReadOnlySequence<byte> buffer, out byte opcode, out int index, out int archive)
    {
        opcode = 0; index = 0; archive = 0;
        if (buffer.Length < 4) return false;

        Span<byte> header = stackalloc byte[4];
        buffer.Slice(0, 4).CopyTo(header);

        opcode = header[0];
        index = header[1];
        archive = (header[2] << 8) | header[3];

        buffer = buffer.Slice(4);
        return true;
    }

    #endregion

    #region Login

    private async Task HandleLoginAsync(Socket socket, CancellationToken ct)
    {
        // Delegate full login protocol to LoginHandler
        var loginResult = await _loginHandler.HandleAsync(socket, ct);
        if (loginResult is null) return;

        var session = loginResult.Session;

        try
        {
            await SendInitialDataAsync(session, ct);
            await RunGameLoopWithPipelinesAsync(session, ct);
        }
        finally
        {
            // Save player state in a fresh scope (DbContext is scoped)
            await using (var saveScope = _scopeFactory.CreateAsyncScope())
            {
                var playerRepo = saveScope.ServiceProvider.GetRequiredService<IPlayerRepository>();
                await playerRepo.SaveAsync(loginResult.Player, ct);
            }
            _world.Unregister(loginResult.Player);
            _sessionManager.Unregister(session);
            session.Dispose();
            _logger.LogInformation("Player {Username} disconnected", loginResult.Player.Username);
        }
    }

    #endregion

    #region Initial Data

    private async Task SendInitialDataAsync(PlayerSession session, CancellationToken ct)
    {
        var player = session.Player;

        await PacketSender.SendMapRegion(session, _protocol, ct);
        player.NeedsMapRegionUpdate = false;
        player.LastKnownRegion = player.Position;

        int[] sidebarInterfaces =
        [
            2423, 3917, 638, 3213, 1644, 5608, 12855, -1,
            5065, 5715, 2449, 904, 147, -1
        ];

        for (int i = 0; i < sidebarInterfaces.Length; i++)
        {
            if (sidebarInterfaces[i] != -1)
                await PacketSender.SendSidebar(session, _protocol, i, sidebarInterfaces[i], ct);
        }

        await PacketSender.SendAllSkills(session, _protocol, ct);
        await PacketSender.SendInventory(session, _protocol, ct);
        await PacketSender.SendEquipment(session, _protocol, ct);
        await PacketSender.SendEnergy(session, _protocol, ct);
        await PacketSender.SendWeight(session, _protocol, ct);

        await PacketSender.SendPlayerOption(session, _protocol, "Follow", 1, false, ct);
        await PacketSender.SendPlayerOption(session, _protocol, "Trade with", 2, false, ct);
        await PacketSender.SendPlayerOption(session, _protocol, "Req Assist", 3, false, ct);

        await PacketSender.SendMessage(session, _protocol, "Welcome to AeroScape.", ct);
        await PacketSender.SendMessage(session, _protocol,
            $"There are currently {_world.PlayerCount} player(s) online.", ct);
    }

    #endregion

    #region Game Packet Loop (System.IO.Pipelines)

    /// <summary>
    /// Runs the game packet loop using System.IO.Pipelines.
    ///
    /// Architecture:
    /// ┌──────────┐     ┌──────────────┐     ┌──────────────────┐
    /// │  Socket   │────▶│  Pipe Writer  │────▶│  Pipe Reader     │
    /// │ (kernel)  │     │  (pooled mem) │     │  (packet parse)  │
    /// └──────────┘     └──────────────┘     └──────────────────┘
    /// </summary>
    private async Task RunGameLoopWithPipelinesAsync(PlayerSession session, CancellationToken ct)
    {
        var pipe = new Pipe(new PipeOptions(
            minimumSegmentSize: 4096,
            pauseWriterThreshold: 128 * 1024,
            resumeWriterThreshold: 64 * 1024,
            useSynchronizationContext: false));

        var fillTask = FillPipeFromSocketAsync(session.Socket, pipe.Writer, ct);
        var processTask = ProcessGamePacketsFromPipeAsync(session, pipe.Reader, ct);

        await Task.WhenAny(fillTask, processTask);

        pipe.Writer.Complete();
        pipe.Reader.Complete();

        try { await fillTask; } catch { }
        try { await processTask; } catch { }
    }

    /// <summary>
    /// Reads game packets from the pipe using a state machine approach.
    /// Dispatches to <see cref="PacketRouter"/> instead of a manual switch.
    /// </summary>
    private async Task ProcessGamePacketsFromPipeAsync(PlayerSession session, PipeReader reader, CancellationToken ct)
    {
        try
        {
            while (session.IsConnected && !ct.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(ct);
                var buffer = result.Buffer;

                var consumed = buffer.Start;
                var examined = buffer.End;

                while (buffer.Length > 0)
                {
                    // --- Step 1: Read opcode (1 byte, ISAAC encrypted) ---
                    if (buffer.Length < 1) break;

                    byte rawOpcode = GetByte(buffer, 0);
                    int opcode = session.IncomingCipher != null
                        ? (rawOpcode - session.IncomingCipher.NextInt()) & 0xFF
                        : rawOpcode;

                    buffer = buffer.Slice(1);

                    var pktDef = _protocol.GetIncoming(opcode);
                    int size = pktDef?.Size ?? 0;

                    // --- Step 2: Read size for variable-length packets ---
                    if (size == -1) // var byte
                    {
                        if (buffer.Length < 1) break;
                        size = GetByte(buffer, 0) & 0xFF;
                        buffer = buffer.Slice(1);
                    }
                    else if (size == -2) // var short
                    {
                        if (buffer.Length < 2) break;
                        size = (GetByte(buffer, 0) << 8 | GetByte(buffer, 1)) & 0xFFFF;
                        buffer = buffer.Slice(2);
                    }

                    // --- Step 3: Read payload ---
                    if (buffer.Length < size) break;

                    byte[] payload;
                    if (size > 0)
                    {
                        payload = new byte[size];
                        buffer.Slice(0, size).CopyTo(payload);
                    }
                    else
                    {
                        payload = [];
                    }
                    buffer = buffer.Slice(size);
                    consumed = buffer.Start;

                    // --- Step 4: Route through PacketRouter ---
                    if (pktDef != null)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        await _packetRouter.RouteAsync(scope.ServiceProvider, session, pktDef, payload, ct);
                    }
                }

                reader.AdvanceTo(consumed, examined);
                if (result.IsCompleted) break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Game pipe ended for {Player}", session.Player.Username);
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }

    private static byte GetByte(ReadOnlySequence<byte> buffer, long offset)
    {
        var reader = new SequenceReader<byte>(buffer);
        if (offset > 0) reader.Advance(offset);
        reader.TryRead(out byte value);
        return value;
    }

    #endregion

    #region Shared Pipe Utilities

    private static async Task FillPipeFromSocketAsync(Socket socket, PipeWriter writer, CancellationToken ct)
    {
        const int MinBufferSize = 4096;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var memory = writer.GetMemory(MinBufferSize);

                int bytesRead;
                try
                {
                    bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, ct);
                }
                catch (SocketException) { break; }
                catch (OperationCanceledException) { break; }

                if (bytesRead == 0) break;

                writer.Advance(bytesRead);
                var result = await writer.FlushAsync(ct);
                if (result.IsCompleted) break;
            }
        }
        catch (Exception) { }
        finally
        {
            await writer.CompleteAsync();
        }
    }

    #endregion
}
