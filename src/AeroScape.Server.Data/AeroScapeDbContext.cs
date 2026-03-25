using AeroScape.Server.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AeroScape.Server.Data;

public sealed class AeroScapeDbContext : DbContext
{
    public DbSet<DbPlayer> Players => Set<DbPlayer>();
    public DbSet<DbSkill> Skills => Set<DbSkill>();
    public DbSet<DbItem> Items => Set<DbItem>();
    public DbSet<DbFriend> Friends => Set<DbFriend>();
    public DbSet<DbIgnore> Ignores => Set<DbIgnore>();

    public AeroScapeDbContext(DbContextOptions<AeroScapeDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbPlayer>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
            
            entity.HasMany(e => e.Skills)
                .WithOne(s => s.Player)
                .HasForeignKey(s => s.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Items)
                .WithOne(i => i.Player)
                .HasForeignKey(i => i.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Friends)
                .WithOne(f => f.Player)
                .HasForeignKey(f => f.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Ignores)
                .WithOne(ig => ig.Player)
                .HasForeignKey(ig => ig.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DbItem>(entity =>
        {
            entity.HasIndex(e => new { e.PlayerId, e.ContainerType, e.Slot });
        });

        modelBuilder.Entity<DbSkill>(entity =>
        {
            entity.HasIndex(e => new { e.PlayerId, e.SkillId }).IsUnique();
        });
    }
}
