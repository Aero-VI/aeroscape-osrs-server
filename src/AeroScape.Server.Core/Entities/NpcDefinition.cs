namespace AeroScape.Server.Core.Entities;

/// <summary>
/// Static NPC definition loaded from data files.
/// </summary>
public sealed class NpcDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = "null";
    public int CombatLevel { get; set; }
    public int Size { get; set; } = 1;
    public bool Attackable { get; set; }
    public int WalkRadius { get; set; }
    public int RespawnTime { get; set; } = 30; // ticks
    public int MaxHealth { get; set; } = 100;
    public string[] Actions { get; set; } = ["Talk-to", null!, null!, null!, "Examine"];
}
