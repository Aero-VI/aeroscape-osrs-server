namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Bounty Hunter minigame actions — entering the crater, leaving, or being assigned a target.
///
/// Translated from legacy Java: DavidScape.io.packets.bountyHunter
/// </summary>
public readonly record struct BountyHunterMessage(BountyHunterAction Action);

/// <summary>
/// Possible Bounty Hunter actions a player can initiate.
/// </summary>
public enum BountyHunterAction
{
    /// <summary>Enter the Bounty Hunter crater.</summary>
    Enter,

    /// <summary>Leave the Bounty Hunter crater (forfeit).</summary>
    Leave,

    /// <summary>Request a new opponent assignment.</summary>
    RequestOpponent,
}
