using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Core.Messages;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Core.Handlers;

/// <summary>
/// Handles Bounty Hunter crater interactions — entering, leaving, opponent matching,
/// and target tracking.
///
/// Translated from legacy Java: DavidScape.io.packets.bountyHunter
///
/// NOTE: The legacy implementation kept mutable state directly on the handler.
/// In the new architecture, persistent minigame state should live in a dedicated
/// BountyHunterService registered in DI.
/// </summary>
public sealed class BountyHunterMessageHandler : IMessageHandler<BountyHunterMessage>
{
    private readonly ILogger<BountyHunterMessageHandler> _logger;

    /// <summary>
    /// Bounding box for the Bounty Hunter crater area.
    /// </summary>
    private const int CraterMinX = 3085;
    private const int CraterMaxX = 3185;
    private const int CraterMinY = 3662;
    private const int CraterMaxY = 3765;

    /// <summary>
    /// Bounty Hunter interface id (tab 8 overlay).
    /// </summary>
    private const int BountyInterfaceId = 653;

    /// <summary>
    /// String child id on the BH interface that displays the opponent's name.
    /// </summary>
    private const int OpponentNameChild = 8;

    /// <summary>
    /// Entry teleport destination.
    /// </summary>
    private static readonly (int X, int Y, int Z) EntryPosition = (3174, 3710, 0);

    /// <summary>
    /// Exit teleport destination (just south of the crater).
    /// </summary>
    private static readonly (int X, int Y, int Z) ExitPosition = (3180, 3685, 0);

    /// <summary>
    /// PK skull icon displayed while inside the crater.
    /// </summary>
    private const int BountySkullIcon = 3;

    public BountyHunterMessageHandler(ILogger<BountyHunterMessageHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask HandleAsync(IPlayerSession session, BountyHunterMessage message, CancellationToken ct = default)
    {
        var player = session.Player;

        switch (message.Action)
        {
            case BountyHunterAction.Enter:
                HandleEnter(session, player);
                break;

            case BountyHunterAction.Leave:
                HandleLeave(session, player);
                break;

            case BountyHunterAction.RequestOpponent:
                HandleRequestOpponent(session, player);
                break;

            default:
                _logger.LogWarning("[{Username}] Unknown BountyHunter action: {Action}",
                    player.Username, message.Action);
                break;
        }

        return ValueTask.CompletedTask;
    }

    private void HandleEnter(IPlayerSession session, Entities.Player player)
    {
        // TODO: Delegate to BountyHunterService:
        //   1. Clear current opponent (bountyOpp = 0)
        //   2. Set tab 8 to BountyInterfaceId (653)
        //   3. Attempt to find an opponent via GetOpponent
        //   4. Set PK skull icon to BountySkullIcon
        //   5. Teleport player to EntryPosition

        _logger.LogDebug("[{Username}] Entering Bounty Hunter crater", player.Username);
    }

    private void HandleLeave(IPlayerSession session, Entities.Player player)
    {
        // TODO: Delegate to BountyHunterService:
        //   1. Notify current opponent that target has left
        //   2. Clear both players' bountyOpp
        //   3. Find a new opponent for the other player
        //   4. Teleport both to ExitPosition

        _logger.LogDebug("[{Username}] Leaving Bounty Hunter crater", player.Username);
    }

    private void HandleRequestOpponent(IPlayerSession session, Entities.Player player)
    {
        // TODO: Delegate to BountyHunterService:
        //   1. Verify player is inside crater bounds and alive
        //   2. Iterate all online players inside the crater
        //   3. Skip players who already have an opponent, or are the same player
        //   4. Assign mutual opponent, update interface strings
        //   5. If none found, display "None Found" on interface

        _logger.LogDebug("[{Username}] Requesting Bounty Hunter opponent", player.Username);
    }

    /// <summary>
    /// Checks whether the given world coordinates fall within the Bounty Hunter crater area.
    /// </summary>
    public static bool IsInCraterArea(int x, int y)
        => x >= CraterMinX && x <= CraterMaxX && y >= CraterMinY && y <= CraterMaxY;
}
