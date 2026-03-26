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
    
    // Inventory: 28 slots, Equipment: 14 slots, Bank: 496 (always stacks)
    public ItemContainer Inventory { get; } = new(28);
    public ItemContainer Equipment { get; } = new(14);
    public ItemContainer Bank { get; } = new(496, alwaysStack: true);
    
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
    public bool EnergyChanged { get; set; }
    
    // Prayer
    public bool[] PrayerActive { get; } = new bool[27];
    public int PrayerIcon { get; set; } = -1;
    public int PrayerDrainRate { get; set; }
    
    // Combat (expanded from legacy PlayerCombat.java / PlayerNPCCombat.java)
    public bool AutoRetaliate { get; set; } = true;
    public int? FollowTargetIndex { get; set; }
    public int CombatDelay { get; set; }
    public int AttackDelay { get; set; } = 5;
    public int AttackStyle { get; set; }    // 0=accurate, 1=strong, 2=block, 3=all-around
    public int AttackEmote { get; set; } = 422;
    public int SpecialAmount { get; set; } = 100;
    public bool UsingSpecial { get; set; }
    public int MagicDelay { get; set; }
    public int FreezeDelay { get; set; }
    public int ClickDelay { get; set; }
    
    // Equipment bonuses (from legacy Player.java)
    public int[] EquipmentBonus { get; } = new int[13];

    // Following (from legacy PlayerFollow.java)
    public int FollowPlayerIndex { get; set; }
    public bool FollowingPlayer { get; set; }

    // Wilderness & PvP state
    public bool IsDead { get; set; }
    public int HeightLevel { get; set; }

    // Skill states (fishing, mining, woodcutting, construction)
    public bool IsFishing { get; set; }
    public int FishTimer { get; set; }
    
    // Vengeance (from legacy)
    public bool VengeanceActive { get; set; }
    public long LastVengeanceTime { get; set; }
    
    // Barrows tracking (from legacy)
    public bool[] Barrows { get; } = new bool[6];
    
    // Interface state
    public int OpenInterfaceId { get; set; } = -1;
    
    // Flags
    public bool IsActive { get; set; }
    
    // Animation / Graphic update flags
    public bool AnimationUpdateRequired { get; set; }
    public bool GraphicUpdateRequired { get; set; }
    public bool ForceChatUpdateRequired { get; set; }
    public bool FaceEntityUpdateRequired { get; set; }
    public bool FaceCoordinateUpdateRequired { get; set; }
    public bool HitUpdateRequired { get; set; }
    public bool Hit2UpdateRequired { get; set; }
    
    // Animation / Graphic data
    public int AnimationId { get; set; } = -1;
    public int AnimationDelay { get; set; }
    public int GraphicId { get; set; } = -1;
    public int GraphicHeight { get; set; }
    public int GraphicDelay { get; set; }
    public string? ForceChat { get; set; }
    public int FaceEntityIndex { get; set; } = -1;
    public int FaceX { get; set; }
    public int FaceY { get; set; }
    public int HitDamage { get; set; }
    public int HitType { get; set; }
    public int Hit2Damage { get; set; }
    public int Hit2Type { get; set; }
    
    // Friends / Ignore
    public List<long> FriendsList { get; } = new(200);
    public List<long> IgnoreList { get; } = new(100);
    
    // Local player/NPC lists for update tracking
    public List<Player> LocalPlayers { get; } = new(256);
    public List<Npc> LocalNpcs { get; } = new(256);
    
    public void PlayAnimation(int animId, int delay = 0)
    {
        AnimationId = animId;
        AnimationDelay = delay;
        AnimationUpdateRequired = true;
        UpdateRequired = true;
    }

    public void PlayGraphic(int graphicId, int height = 0, int delay = 0)
    {
        GraphicId = graphicId;
        GraphicHeight = height;
        GraphicDelay = delay;
        GraphicUpdateRequired = true;
        UpdateRequired = true;
    }

    public void ForceMessage(string text)
    {
        ForceChat = text;
        ForceChatUpdateRequired = true;
        UpdateRequired = true;
    }

    public void FaceEntity(int entityIndex)
    {
        FaceEntityIndex = entityIndex;
        FaceEntityUpdateRequired = true;
        UpdateRequired = true;
    }

    public void FacePosition(Position pos)
    {
        FaceX = pos.X * 2 + 1;
        FaceY = pos.Y * 2 + 1;
        FaceCoordinateUpdateRequired = true;
        UpdateRequired = true;
    }
    
    public void ResetFlags()
    {
        UpdateRequired = false;
        AppearanceUpdateRequired = false;
        ChatUpdateRequired = false;
        AnimationUpdateRequired = false;
        GraphicUpdateRequired = false;
        ForceChatUpdateRequired = false;
        FaceEntityUpdateRequired = false;
        FaceCoordinateUpdateRequired = false;
        HitUpdateRequired = false;
        Hit2UpdateRequired = false;
        EnergyChanged = false;
        WalkDirection = -1;
        RunDirection = -1;
        IsTeleporting = false;
        ChatText = null;
        ForceChat = null;
    }

    /// <summary>Per-tick decay for combat timers (from legacy process methods).</summary>
    public void ProcessTimers()
    {
        if (CombatDelay > 0) CombatDelay--;
        if (MagicDelay > 0) MagicDelay--;
        if (FreezeDelay > 0) FreezeDelay--;
        if (ClickDelay > 0) ClickDelay--;
        if (FishTimer > 0) FishTimer--;
    }
}
