namespace AeroScape.Server.Core.Messages;

/// <summary>
/// Barbarian Assault minigame action — player entering a wave lobby or related interaction.
/// </summary>
public readonly record struct AssaultMessage(AssaultAction Action, int Wave);

/// <summary>
/// The type of Barbarian Assault action being requested.
/// </summary>
public enum AssaultAction
{
    /// <summary>Player is entering/leaving the wave lobby.</summary>
    EnterWave,

    /// <summary>An NPC in the assault arena has died.</summary>
    NpcDied,

    /// <summary>A player in the assault arena has died.</summary>
    PlayerDied,

    /// <summary>Force-end the current game.</summary>
    EndGame,
}
