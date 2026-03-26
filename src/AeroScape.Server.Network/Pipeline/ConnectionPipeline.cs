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

    /// <summary>
    /// RS2 508 Update (JS5) handshake keys — sent after the initial version acknowledgement.
    /// The client reads these before transitioning to JS5 cache request mode.
    /// Sourced from the legacy Java server (Misc.uKeys).
    /// </summary>
    private static readonly byte[] UpdateKeys =
    [
        0xff, 0x00, 0xff, 0x00, 0x00, 0x00, 0x00, 0xd8, 0x84, 0xa1, 0xa1, 0x2b,
        0x00, 0x00, 0x00, 0xba, 0x58, 0x64, 0xe8, 0x14, 0x00, 0x00, 0x00, 0x7b,
        0xcc, 0xa0, 0x7e, 0x23, 0x00, 0x00, 0x00, 0x48, 0x20, 0x0e, 0xe3, 0x6e,
        0x00, 0x00, 0x01, 0x88, 0xec, 0x0d, 0x58, 0xed, 0x00, 0x00, 0x00, 0x71,
        0xb9, 0x4c, 0xc0, 0x50, 0x00, 0x00, 0x01, 0x8b, 0x5b, 0x61, 0x79, 0x20,
        0x00, 0x00, 0x00, 0x0c, 0x0c, 0x69, 0xb1, 0xc8, 0x00, 0x00, 0x02, 0x31,
        0xc8, 0x56, 0x67, 0x52, 0x00, 0x00, 0x00, 0x69, 0x78, 0x17, 0x7b, 0xe2,
        0x00, 0x00, 0x00, 0xc3, 0x29, 0x76, 0x27, 0x6a, 0x00, 0x00, 0x00, 0x05,
        0x44, 0xe7, 0x75, 0xcb, 0x00, 0x00, 0x00, 0x08, 0x7d, 0x21, 0x80, 0xd5,
        0x00, 0x00, 0x01, 0x58, 0xeb, 0x7d, 0x49, 0x8e, 0x00, 0x00, 0x00, 0x0c,
        0xf4, 0xdf, 0xd6, 0x4d, 0x00, 0x00, 0x00, 0x18, 0xec, 0x33, 0x31, 0x7e,
        0x00, 0x00, 0x00, 0x01, 0xf7, 0x7a, 0x09, 0xe3, 0x00, 0x00, 0x00, 0xd7,
        0xe6, 0xa7, 0xa5, 0x18, 0x00, 0x00, 0x00, 0x45, 0xb5, 0x0a, 0xe0, 0x64,
        0x00, 0x00, 0x00, 0x75, 0xba, 0xf2, 0xa2, 0xb9, 0x00, 0x00, 0x00, 0x5f,
        0x31, 0xff, 0xfd, 0x16, 0x00, 0x00, 0x01, 0x48, 0x03, 0xf5, 0x55, 0xab,
        0x00, 0x00, 0x00, 0x1e, 0x85, 0x03, 0x5e, 0xa7, 0x00, 0x00, 0x00, 0x23,
        0x4e, 0x81, 0xae, 0x7d, 0x00, 0x00, 0x00, 0x18, 0x67, 0x07, 0x33, 0xe3,
        0x00, 0x00, 0x00, 0x14, 0xab, 0x81, 0x05, 0xac, 0x00, 0x00, 0x00, 0x03,
        0x24, 0x75, 0x85, 0x14, 0x00, 0x00, 0x00, 0x36
    ];

    private async Task HandleUpdateRequestAsync(Socket socket, CancellationToken ct)
    {
        var buffer = new byte[8];
        int read = await socket.ReceiveAsync(buffer.AsMemory(0, 4), SocketFlags.None, ct);
        if (read < 4) return;

        int clientVersion = BinaryPrimitives.ReadInt32BigEndian(buffer);

        if (clientVersion != ServerConstants.Revision)
        {
            _logger.LogWarning("JS5 version mismatch: client={ClientVer}, server={ServerVer}",
                clientVersion, ServerConstants.Revision);
            await socket.SendAsync(new byte[] { 6 }, SocketFlags.None, ct);
            return;
        }

        // Step 1: Acknowledge version — send 0 (success)
        await socket.SendAsync(new byte[] { 0 }, SocketFlags.None, ct);

        // Step 2: Client sends 8 padding bytes, server responds with update keys.
        // The legacy Java server reads 8 bytes then writes the entire uKeys array.
        int padRead = 0;
        while (padRead < 8)
        {
            int n = await socket.ReceiveAsync(buffer.AsMemory(padRead, 8 - padRead), SocketFlags.None, ct);
            if (n == 0) return;
            padRead += n;
        }

        await socket.SendAsync(UpdateKeys, SocketFlags.None, ct);

        _logger.LogDebug("JS5 handshake complete — sent {Len} update key bytes", UpdateKeys.Length);

        // Step 3: Serve JS5 cache requests
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
    ///
    /// CRITICAL: ISAAC values must only be consumed when the full packet is
    /// available in the buffer. If we consume an ISAAC value for the opcode
    /// but then discover we don't have enough bytes for the payload, the
    /// ISAAC stream becomes permanently desynchronized from the client.
    /// We solve this by peeking at the buffer to calculate the total frame
    /// size BEFORE consuming any ISAAC values.
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
                    // --- Peek at opcode to determine frame size BEFORE consuming ISAAC ---
                    if (buffer.Length < 1) break;

                    byte rawOpcode = GetByte(buffer, 0);

                    // Peek-decrypt the opcode to look up the packet definition.
                    // We must NOT call NextInt() yet — just peek at what the opcode would be.
                    // To peek without consuming, we calculate what the decrypted value would be
                    // by using a temporary approach: we need to know the frame size first.
                    //
                    // Strategy: compute total bytes needed for this frame, then only consume
                    // the ISAAC value once we're sure the entire frame is buffered.

                    // Tentatively decrypt to look up the packet definition.
                    // We'll consume the ISAAC value for real only after confirming the full frame fits.
                    int opcode = session.IncomingCipher != null
                        ? (rawOpcode - session.IncomingCipher.PeekNextInt()) & 0xFF
                        : rawOpcode;

                    var pktDef = _protocol.GetIncoming(opcode);
                    int declaredSize = pktDef?.Size ?? 0;

                    // Calculate total frame length (opcode + optional size header + payload)
                    int headerSize = 1; // opcode byte
                    int payloadSize;

                    if (declaredSize == -1) // var byte
                    {
                        headerSize += 1;
                        if (buffer.Length < headerSize) break;
                        payloadSize = GetByte(buffer, 1) & 0xFF;
                    }
                    else if (declaredSize == -2) // var short
                    {
                        headerSize += 2;
                        if (buffer.Length < headerSize) break;
                        payloadSize = ((GetByte(buffer, 1) << 8) | GetByte(buffer, 2)) & 0xFFFF;
                    }
                    else
                    {
                        payloadSize = declaredSize;
                    }

                    int totalFrameSize = headerSize + payloadSize;

                    // Ensure the entire frame is buffered before consuming anything
                    if (buffer.Length < totalFrameSize) break;

                    // --- NOW it's safe to consume the ISAAC value ---
                    if (session.IncomingCipher != null)
                        session.IncomingCipher.NextInt(); // consume the value we peeked

                    // Advance past header
                    buffer = buffer.Slice(headerSize);

                    // --- Read payload ---
                    byte[] payload;
                    if (payloadSize > 0)
                    {
                        payload = new byte[payloadSize];
                        buffer.Slice(0, payloadSize).CopyTo(payload);
                    }
                    else
                    {
                        payload = [];
                    }
                    buffer = buffer.Slice(payloadSize);
                    consumed = buffer.Start;

                    // --- Route through PacketRouter ---
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
