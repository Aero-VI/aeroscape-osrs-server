namespace AeroScape.Server.Core.Entities;

public readonly record struct Position(int X, int Y, int Z = 0)
{
    public int RegionX => (X >> 3) - 6;
    public int RegionY => (Y >> 3) - 6;
    public int LocalX => X - 8 * RegionX;
    public int LocalY => Y - 8 * RegionY;
    public int ChunkX => X >> 3;
    public int ChunkY => Y >> 3;

    public Position Delta(Position other) =>
        new(X - other.X, Y - other.Y, Z - other.Z);

    public bool WithinDistance(Position other, int distance = 15) =>
        Math.Abs(X - other.X) <= distance && Math.Abs(Y - other.Y) <= distance;

    public static Position Default => new(3222, 3222, 0); // Lumbridge
}
