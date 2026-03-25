using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AeroScape.Server.Data.Models;

public enum ItemContainerType
{
    Inventory = 0,
    Equipment = 1,
    Bank = 2
}

[Table("Items")]
public sealed class DbItem
{
    [Key]
    public int Id { get; set; }

    public int PlayerId { get; set; }

    [ForeignKey(nameof(PlayerId))]
    public DbPlayer Player { get; set; } = null!;

    public ItemContainerType ContainerType { get; set; }
    public int Slot { get; set; }
    public int ItemId { get; set; }
    public int Amount { get; set; } = 1;
}
