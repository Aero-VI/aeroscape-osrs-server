using AeroScape.Server.Core.Entities;

namespace AeroScape.Server.Core.Game;

/// <summary>
/// Player following — ported from legacy Java PlayerFollow.java.
/// Implements the same pathfinding logic used for following other players
/// during combat, trading, and manual follow.
/// </summary>
public static class PlayerFollowService
{
    /// <summary>
    /// Process one tick of following for a player.
    /// Returns the walk destination, or null if following should stop.
    /// </summary>
    public static Position? GetFollowDestination(Player follower, Player target)
    {
        if (target == null || !target.IsActive || target.IsDead)
            return null;

        int dx = target.Position.X - follower.Position.X;
        int dy = target.Position.Y - follower.Position.Y;

        // If target is more than 12 squares away, reset (from legacy — prevents teleport follow)
        if (Math.Abs(dx) > 12 || Math.Abs(dy) > 12)
            return follower.Position; // Stay in place

        // Legacy pathfinding algorithm from PlayerFollow.java
        int n = dy;
        int targetX, targetY;

        if (dx <= -n && dx >= n - 1)
        {
            // Approach from north/south
            if (n < 0)
            {
                targetX = target.Position.X;
                targetY = target.Position.Y + 1;
            }
            else
            {
                targetX = target.Position.X;
                targetY = target.Position.Y - 1;
            }
        }
        else if (dx > 0)
        {
            targetX = target.Position.X - 1;
            targetY = target.Position.Y;
        }
        else
        {
            targetX = target.Position.X + 1;
            targetY = target.Position.Y;
        }

        // Special cases from legacy for direct N/S alignment
        if (dx == 0 && n < 0)
        {
            targetX = target.Position.X;
            targetY = target.Position.Y + 1;
        }
        else if (dx == 0 && n >= 0)
        {
            targetX = target.Position.X;
            targetY = target.Position.Y - 1;
        }

        return new Position(targetX, targetY, follower.Position.Z);
    }
}
