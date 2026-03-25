namespace AeroScape.Server.Core.Entities;

/// <summary>
/// Core NPC entity — protocol-agnostic game state.
/// </summary>
public sealed class Npc
{
    public int Index { get; set; } = -1;
    public int Id { get; set; }
    public string Name { get; set; } = "NPC";
    
    public Position Position { get; set; }
    public Position SpawnPosition { get; set; }
    public bool IsActive { get; set; }
    
    // Movement
    public int WalkDirection { get; set; } = -1;
    public int RunDirection { get; set; } = -1;
    public bool UpdateRequired { get; set; }
    
    // Update flags
    public bool AnimationUpdateRequired { get; set; }
    public bool GraphicUpdateRequired { get; set; }
    public bool ForceChatUpdateRequired { get; set; }
    public bool FaceEntityUpdateRequired { get; set; }
    public bool FaceCoordinateUpdateRequired { get; set; }
    public bool HitUpdateRequired { get; set; }
    public bool TransformUpdateRequired { get; set; }
    
    // Update data
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
    public int CurrentHealth { get; set; } = 100;
    public int MaxHealth { get; set; } = 100;
    public int TransformId { get; set; } = -1;
    
    // Combat
    public int CombatLevel { get; set; }
    
    // Walking range
    public int WalkRadius { get; set; }
    
    public Npc(int id, Position position)
    {
        Id = id;
        Position = position;
        SpawnPosition = position;
    }

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
        AnimationUpdateRequired = false;
        GraphicUpdateRequired = false;
        ForceChatUpdateRequired = false;
        FaceEntityUpdateRequired = false;
        FaceCoordinateUpdateRequired = false;
        HitUpdateRequired = false;
        TransformUpdateRequired = false;
        WalkDirection = -1;
        RunDirection = -1;
        ForceChat = null;
    }
}
