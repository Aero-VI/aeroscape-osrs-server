using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AeroScape.Server.Data.Models;

[Table("Skills")]
public sealed class DbSkill
{
    [Key]
    public int Id { get; set; }

    public int PlayerId { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public DbPlayer Player { get; set; } = null!;

    public int SkillId { get; set; }
    public int Level { get; set; } = 1;
    public int Experience { get; set; }
}
