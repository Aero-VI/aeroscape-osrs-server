using AeroScape.Server.Core.Entities;

namespace AeroScape.Server.Core.Game;

/// <summary>
/// Handles NPC random walking within their spawn radius.
/// Called each game tick.
/// </summary>
public static class NpcMovementService
{
    /// <summary>
    /// Process NPC movement for all active NPCs.
    /// NPCs with WalkRadius > 0 will randomly walk within range of their spawn.
    /// </summary>
    public static void ProcessAll(GameWorld world)
    {
        foreach (var npc in world.GetActiveNpcs())
        {
            if (npc.WalkRadius <= 0) continue;
            
            // ~10% chance to move each tick (about once every 6 seconds)
            if (Random.Shared.Next(10) != 0) continue;

            var direction = Random.Shared.Next(8);
            var (dx, dy) = DirectionUtil.DeltaForDirection(direction);
            
            var newPos = new Position(
                npc.Position.X + dx,
                npc.Position.Y + dy,
                npc.Position.Z);

            // Check if within walk radius of spawn
            if (Math.Abs(newPos.X - npc.SpawnPosition.X) > npc.WalkRadius ||
                Math.Abs(newPos.Y - npc.SpawnPosition.Y) > npc.WalkRadius)
            {
                continue;
            }

            npc.WalkDirection = direction;
            npc.Position = newPos;
            npc.UpdateRequired = true;
        }
    }
}
