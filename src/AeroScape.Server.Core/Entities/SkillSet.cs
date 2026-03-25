namespace AeroScape.Server.Core.Entities;

public sealed class SkillSet
{
    public const int SkillCount = 25;
    
    public static readonly string[] SkillNames =
    [
        "Attack", "Defence", "Strength", "Hitpoints", "Ranged",
        "Prayer", "Magic", "Cooking", "Woodcutting", "Fletching",
        "Fishing", "Firemaking", "Crafting", "Smithing", "Mining",
        "Herblore", "Agility", "Thieving", "Slayer", "Farming",
        "Runecrafting", "Hunter", "Construction", "Summoning", "Dungeoneering"
    ];

    private readonly int[] _levels = new int[SkillCount];
    private readonly int[] _experience = new int[SkillCount];

    public SkillSet()
    {
        // Default: all level 1, Hitpoints level 10
        for (int i = 0; i < SkillCount; i++)
        {
            _levels[i] = 1;
            _experience[i] = 0;
        }
        _levels[3] = 10; // Hitpoints
        _experience[3] = 1184; // XP for level 10
    }

    public int GetLevel(int skillId) => _levels[skillId];
    public int GetExperience(int skillId) => _experience[skillId];
    
    public void SetLevel(int skillId, int level) => _levels[skillId] = level;
    public void SetExperience(int skillId, int experience) => _experience[skillId] = experience;

    public int GetLevelForExperience(int experience)
    {
        int points = 0;
        for (int level = 1; level <= 99; level++)
        {
            points += (int)(level + 300.0 * Math.Pow(2.0, level / 7.0));
            if (points / 4 >= experience)
                return level;
        }
        return 99;
    }

    public void AddExperience(int skillId, int amount)
    {
        _experience[skillId] = Math.Min(_experience[skillId] + amount, 200_000_000);
        int newLevel = GetLevelForExperience(_experience[skillId]);
        if (newLevel > _levels[skillId])
            _levels[skillId] = newLevel;
    }

    public int CombatLevel
    {
        get
        {
            double @base = (GetLevel(1) + GetLevel(3)) * 0.25 + 1.3; // Defence + Hitpoints
            double melee = (GetLevel(0) + GetLevel(2)) * 0.325; // Attack + Strength
            double ranged = GetLevel(4) * 0.4875;
            double magic = GetLevel(6) * 0.4875;
            double prayer = GetLevel(5) * 0.125;
            return (int)(@base + Math.Max(melee, Math.Max(ranged, magic)) + prayer);
        }
    }

    public int[] GetAllLevels() => (int[])_levels.Clone();
    public int[] GetAllExperience() => (int[])_experience.Clone();
}
