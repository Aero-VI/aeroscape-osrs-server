namespace AeroScape.Server.Core.Entities;

public sealed class Appearance
{
    public int Gender { get; set; }
    public int[] Look { get; set; } = [0, 10, 18, 26, 33, 36, 42]; // Default male
    public int[] Colors { get; set; } = [0, 0, 0, 0, 0];

    public static Appearance Default => new()
    {
        Gender = 0,
        Look = [0, 10, 18, 26, 33, 36, 42],
        Colors = [0, 0, 0, 0, 0]
    };
}
