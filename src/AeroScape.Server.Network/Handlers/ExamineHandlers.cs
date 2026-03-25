using AeroScape.Server.Core.Game;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using AeroScape.Server.Network.Protocol;
using AeroScape.Server.Network.Session;
using AeroScape.Server.Network.Updating;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Network.Handlers;

/// <summary>
/// Handles examining an inventory/ground item to display its description.
/// </summary>
public sealed class ExamineItemHandler : IMessageHandler<ExamineItemMessage>
{
    private readonly ItemDefinitionService _itemDefs;
    private readonly ProtocolService _protocol;
    private readonly ILogger<ExamineItemHandler> _logger;

    public ExamineItemHandler(ItemDefinitionService itemDefs, ProtocolService protocol, ILogger<ExamineItemHandler> logger)
    {
        _itemDefs = itemDefs;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, ExamineItemMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;

        var def = _itemDefs.Get(message.ItemId);
        var description = def?.Examine ?? $"It's item #{message.ItemId}.";

        _logger.LogTrace("Player {Name} examined item {Id}: {Desc}",
            ps.Player.Username, message.ItemId, description);

        await PacketSender.SendMessage(ps, _protocol, description, ct);
    }
}

/// <summary>
/// Handles examining an NPC to display its description and combat level.
/// </summary>
public sealed class ExamineNpcHandler : IMessageHandler<ExamineNpcMessage>
{
    private readonly GameWorld _world;
    private readonly ProtocolService _protocol;
    private readonly ILogger<ExamineNpcHandler> _logger;

    public ExamineNpcHandler(GameWorld world, ProtocolService protocol, ILogger<ExamineNpcHandler> logger)
    {
        _world = world;
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, ExamineNpcMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;

        // NPC definitions lookup — for now use a basic message
        var description = $"It's a creature. (NPC: {message.NpcId})";

        _logger.LogTrace("Player {Name} examined NPC {Id}", ps.Player.Username, message.NpcId);

        await PacketSender.SendMessage(ps, _protocol, description, ct);
    }
}

/// <summary>
/// Handles examining a world object (trees, rocks, doors, etc.).
/// </summary>
public sealed class ExamineObjectHandler : IMessageHandler<ExamineObjectMessage>
{
    private readonly ProtocolService _protocol;
    private readonly ILogger<ExamineObjectHandler> _logger;

    public ExamineObjectHandler(ProtocolService protocol, ILogger<ExamineObjectHandler> logger)
    {
        _protocol = protocol;
        _logger = logger;
    }

    public async ValueTask HandleAsync(IPlayerSession session, ExamineObjectMessage message, CancellationToken ct)
    {
        if (session is not PlayerSession ps) return;

        var description = $"It's an object. (Object: {message.ObjectId})";

        _logger.LogTrace("Player {Name} examined object {Id}", ps.Player.Username, message.ObjectId);

        await PacketSender.SendMessage(ps, _protocol, description, ct);
    }
}
