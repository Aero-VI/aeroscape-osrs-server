namespace AeroScape.Server.Core.Entities;

/// <summary>
/// Core player entity — protocol-agnostic game state.
/// No knowledge of sockets, packets, or byte buffers.
/// </summary>
public sealed class Player
{
    public int Index { get; set; } = -1;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Rights { get; set; }
    
    public Position Position { get; set; } = Position.Default;
    public Position LastKnownRegion { get; set; } = Position.Default;
    public bool NeedsMapRegionUpdate { get; set; } = true;
    public bool UpdateRequired { get; set; } = true;
    public bool AppearanceUpdateRequired { get; set; } = true;
    public bool ChatUpdateRequired { get; set; }
    
    public Appearance Appearance { get; set; } = Appearance.Default;
    public SkillSet Skills { get; } = new();
    
    // Inventory: 28 slots, Equipment: 14 slots
    public ItemContainer Inventory { get; } = new(28);
    public ItemContainer Equipment { get; } = new(14);
    public ItemContainer Bank { get; } = new(496);
    
    // Movement
    public int WalkDirection { get; set; } = -1;
    public int RunDirection { get; set; } = -1;
    public bool IsRunning { get; set; }
    public bool IsTeleporting { get; set; }
    
    // Chat
    public int ChatColor { get; set; }
    public int ChatEffect { get; set; }
    public byte[]? ChatText { get; set; }
    
    // Energy
    public int RunEnergy { get; set; } = 100;
    
    // Flags
    public bool IsActive { get; set; }
    
    public void ResetFlags()
    {
        UpdateRequired = false;
        AppearanceUpdateRequired = false;
        ChatUpdateRequired = false;
        WalkDirection = -1;
        RunDirection = -1;
        IsTeleporting = false;
        ChatText = null;
    }
}
