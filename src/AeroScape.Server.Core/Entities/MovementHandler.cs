namespace AeroScape.Server.Core.Entities;

/// <summary>
/// Queued waypoint walking for a player.
/// Ported from typical Java RSPS walking queue logic.
/// </summary>
public sealed class MovementHandler
{
    private readonly Queue<Position> _waypoints = new();

    public void Reset() => _waypoints.Clear();

    public void AddStep(Position step)
    {
        _waypoints.Enqueue(step);
    }

    /// <summary>
    /// Processes one game tick of movement for the player.
    /// Returns true if the player moved.
    /// </summary>
    public bool Process(Player player)
    {
        player.WalkDirection = -1;
        player.RunDirection = -1;

        if (_waypoints.Count == 0)
            return false;

        var current = player.Position;
        var walkPoint = _waypoints.Dequeue();
        int walkDir = DirectionUtil.GetDirection(current, walkPoint);

        if (walkDir == -1)
            return false;

        player.WalkDirection = walkDir;
        player.Position = walkPoint;
        player.UpdateRequired = true;

        // Running: take a second step
        if (player.IsRunning && _waypoints.Count > 0 && player.RunEnergy > 0)
        {
            var runPoint = _waypoints.Dequeue();
            int runDir = DirectionUtil.GetDirection(player.Position, runPoint);
            if (runDir != -1)
            {
                player.RunDirection = runDir;
                player.Position = runPoint;
                player.RunEnergy--;
            }
        }

        return true;
    }

    public bool HasSteps => _waypoints.Count > 0;
}

public static class DirectionUtil
{
    private static readonly int[,] DirectionMap =
    {
        { 1, 2, 4 },
        { 0, -1, 7 },
        { 6, 5, 3 }
    };

    public static int GetDirection(Position from, Position to)
    {
        int dx = Math.Sign(to.X - from.X);
        int dy = Math.Sign(to.Y - from.Y);
        if (dx == 0 && dy == 0) return -1;
        return DirectionMap[1 - dy, dx + 1];
    }

    public static (int dx, int dy) DeltaForDirection(int direction)
    {
        return direction switch
        {
            0 => (-1, 0),
            1 => (-1, 1),
            2 => (0, 1),
            3 => (1, 1),
            4 => (1, 0),  // originally was (0, 1)? Let me use standard RS directions
            5 => (1, -1),
            6 => (-1, -1),
            7 => (0, -1),
            _ => (0, 0)
        };
    }
}
