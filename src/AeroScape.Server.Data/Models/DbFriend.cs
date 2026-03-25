using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AeroScape.Server.Data.Models;

[Table("Friends")]
public sealed class DbFriend
{
    [Key]
    public int Id { get; set; }

    public int PlayerId { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public DbPlayer Player { get; set; } = null!;

    public long FriendNameLong { get; set; }
}

[Table("Ignores")]
public sealed class DbIgnore
{
    [Key]
    public int Id { get; set; }

    public int PlayerId { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public DbPlayer Player { get; set; } = null!;

    public long IgnoreNameLong { get; set; }
}
