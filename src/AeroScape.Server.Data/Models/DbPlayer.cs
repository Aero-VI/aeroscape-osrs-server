using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AeroScape.Server.Data.Models;

[Table("Players")]
public sealed class DbPlayer
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(12)]
    public string Username { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    public int Rights { get; set; }

    // Position
    public int PositionX { get; set; } = 3222;
    public int PositionY { get; set; } = 3222;
    public int PositionZ { get; set; }

    // Appearance
    public int Gender { get; set; }

    [MaxLength(64)]
    public string LookJson { get; set; } = "[0,10,18,26,33,36,42]";

    [MaxLength(64)]
    public string ColorsJson { get; set; } = "[0,0,0,0,0]";

    // Energy
    public int RunEnergy { get; set; } = 100;
    public bool IsRunning { get; set; }

    // Navigation properties
    public ICollection<DbSkill> Skills { get; set; } = new List<DbSkill>();
    public ICollection<DbItem> InventoryItems { get; set; } = new List<DbItem>();
    public ICollection<DbItem> EquipmentItems { get; set; } = new List<DbItem>();
    public ICollection<DbItem> BankItems { get; set; } = new List<DbItem>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLogin { get; set; } = DateTime.UtcNow;
}
