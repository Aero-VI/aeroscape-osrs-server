using System.Collections.Frozen;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Network.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Protocol;

/// <summary>
/// Route entry: knows how to decode raw bytes into a message and dispatch
/// to the matching IMessageHandler&lt;T&gt;. One entry per packet-name family.
/// </summary>
public interface IPacketRoute
{
    ValueTask DispatchAsync(IServiceProvider sp, PlayerSession session, string packetName, byte[] payload, CancellationToken ct);
}

/// <summary>
/// Generic route implementation — binds a decoder + handler pair at registration time.
/// </summary>
internal sealed class PacketRoute<TMessage> : IPacketRoute where TMessage : struct
{
    private readonly IPacketDecoder<TMessage> _decoder;

    public PacketRoute(IPacketDecoder<TMessage> decoder) => _decoder = decoder;

    public async ValueTask DispatchAsync(IServiceProvider sp, PlayerSession session, string packetName, byte[] payload, CancellationToken ct)
    {
        var message = _decoder.Decode(packetName, payload);
        var handler = sp.GetService<IMessageHandler<TMessage>>();
        if (handler is not null)
            await handler.HandleAsync(session, message, ct);
    }
}

/// <summary>
/// Packet router: maps packet names (from Protocol_508.json) to their
/// decoder + handler pair. Built once at startup via <see cref="Build"/>,
/// then used on every incoming packet in the hot loop.
///
/// Replaces the manual 40-case switch in the old PacketDispatcher.
/// Adding a new packet requires only:
///   1. A message record in Core/Messages
///   2. An IPacketDecoder&lt;T&gt; implementation (Network/Decoders)
///   3. An IMessageHandler&lt;T&gt; implementation (Network/Handlers)
///   4. DI registration — the router discovers everything automatically.
/// </summary>
public sealed class PacketRouter
{
    private readonly FrozenDictionary<string, IPacketRoute> _routes;
    private readonly FrozenSet<string> _silenced;
    private readonly ILogger<PacketRouter> _logger;

    private PacketRouter(FrozenDictionary<string, IPacketRoute> routes, FrozenSet<string> silenced, ILogger<PacketRouter> logger)
    {
        _routes = routes;
        _silenced = silenced;
        _logger = logger;
    }

    /// <summary>
    /// Dispatches a single incoming packet. Called from the connection pipeline.
    /// </summary>
    public async ValueTask RouteAsync(IServiceProvider scopedProvider, PlayerSession session, PacketDefinition pktDef, byte[] payload, CancellationToken ct)
    {
        if (_routes.TryGetValue(pktDef.Name, out var route))
        {
            try
            {
                await route.DispatchAsync(scopedProvider, session, pktDef.Name, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling packet {Name} (opcode {Opcode})", pktDef.Name, pktDef.Opcode);
            }
        }
        else if (!_silenced.Contains(pktDef.Name))
        {
            _logger.LogTrace("Unhandled packet: {Name} (opcode={Opcode}, size={Size})",
                pktDef.Name, pktDef.Opcode, payload.Length);
        }
    }

    /// <summary>
    /// Builds the router from all registered <see cref="IPacketDecoder"/> instances in DI.
    /// Called once at startup.
    /// </summary>
    public static PacketRouter Build(IServiceProvider rootProvider, ILogger<PacketRouter> logger)
    {
        var decoders = rootProvider.GetServices<IPacketDecoder>().ToList();
        var routes = new Dictionary<string, IPacketRoute>();

        foreach (var decoder in decoders)
        {
            // Use reflection to find the closed generic IPacketDecoder<T>
            var decoderType = decoder.GetType();
            var iface = decoderType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPacketDecoder<>));

            if (iface is null) continue;

            var messageType = iface.GetGenericArguments()[0];
            var routeType = typeof(PacketRoute<>).MakeGenericType(messageType);
            var route = (IPacketRoute)Activator.CreateInstance(routeType, decoder)!;

            foreach (var name in decoder.PacketNames)
            {
                routes[name] = route;
                logger.LogDebug("Registered route: {PacketName} → {Decoder} → {Message}",
                    name, decoderType.Name, messageType.Name);
            }
        }

        // Packets we intentionally ignore (anti-cheat telemetry etc.)
        var silenced = new[] { "CameraMoved", "MouseClick" }.ToFrozenSet();

        logger.LogInformation("PacketRouter built: {Count} packet routes registered", routes.Count);
        return new PacketRouter(routes.ToFrozenDictionary(), silenced, logger);
    }
}
