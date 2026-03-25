using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AeroScape.Server.Core.Entities;
using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AeroScape.Server.Data.Repositories;

public sealed class EfPlayerRepository : IPlayerRepository
{
    private readonly AeroScapeDbContext _db;
    private readonly ILogger<EfPlayerRepository> _logger;

    public EfPlayerRepository(AeroScapeDbContext db, ILogger<EfPlayerRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> ExistsAsync(string username, CancellationToken ct) =>
        await _db.Players.AnyAsync(p => p.Username == username, ct);

    public async Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken ct)
    {
        var dbPlayer = await _db.Players.FirstOrDefaultAsync(p => p.Username == username, ct);
        if (dbPlayer is null) return false;
        return dbPlayer.PasswordHash == HashPassword(password);
    }

    public async Task CreateAsync(string username, string password, CancellationToken ct)
    {
        var dbPlayer = new DbPlayer
        {
            Username = username,
            PasswordHash = HashPassword(password),
            PositionX = 3222,
            PositionY = 3222,
        };

        _db.Players.Add(dbPlayer);
        await _db.SaveChangesAsync(ct);

        for (int i = 0; i < SkillSet.SkillCount; i++)
        {
            _db.Skills.Add(new DbSkill
            {
                PlayerId = dbPlayer.Id,
                SkillId = i,
                Level = i == 3 ? 10 : 1,
                Experience = i == 3 ? 1184 : 0
            });
        }
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created new player: {Username}", username);
    }

    public async Task<Player?> LoadAsync(string username, CancellationToken ct)
    {
        var dbPlayer = await _db.Players
            .Include(p => p.Skills)
            .Include(p => p.Items)
            .Include(p => p.Friends)
            .Include(p => p.Ignores)
            .FirstOrDefaultAsync(p => p.Username == username, ct);

        if (dbPlayer is null) return null;

        var player = new Player
        {
            Username = dbPlayer.Username,
            Password = "",
            Rights = dbPlayer.Rights,
            Position = new Position(dbPlayer.PositionX, dbPlayer.PositionY, dbPlayer.PositionZ),
            RunEnergy = dbPlayer.RunEnergy,
            IsRunning = dbPlayer.IsRunning,
            Appearance = new Appearance
            {
                Gender = dbPlayer.Gender,
                Look = DeserializeIntArray(dbPlayer.LookJson) ?? [0, 10, 18, 26, 33, 36, 42],
                Colors = DeserializeIntArray(dbPlayer.ColorsJson) ?? [0, 0, 0, 0, 0]
            }
        };

        foreach (var skill in dbPlayer.Skills)
        {
            player.Skills.SetLevel(skill.SkillId, skill.Level);
            player.Skills.SetExperience(skill.SkillId, skill.Experience);
        }

        foreach (var item in dbPlayer.Items)
        {
            var container = item.ContainerType switch
            {
                ItemContainerType.Inventory => player.Inventory,
                ItemContainerType.Equipment => player.Equipment,
                ItemContainerType.Bank => player.Bank,
                _ => null
            };
            container?.Set(item.Slot, new Item(item.ItemId, item.Amount));
        }

        // Friends and ignores
        foreach (var friend in dbPlayer.Friends)
            player.FriendsList.Add(friend.FriendNameLong);
        foreach (var ignore in dbPlayer.Ignores)
            player.IgnoreList.Add(ignore.IgnoreNameLong);

        dbPlayer.LastLogin = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Loaded player: {Username}", username);
        return player;
    }

    public async Task SaveAsync(Player player, CancellationToken ct)
    {
        var dbPlayer = await _db.Players
            .Include(p => p.Skills)
            .Include(p => p.Items)
            .Include(p => p.Friends)
            .Include(p => p.Ignores)
            .FirstOrDefaultAsync(p => p.Username == player.Username, ct);

        if (dbPlayer is null) return;

        dbPlayer.PositionX = player.Position.X;
        dbPlayer.PositionY = player.Position.Y;
        dbPlayer.PositionZ = player.Position.Z;
        dbPlayer.RunEnergy = player.RunEnergy;
        dbPlayer.IsRunning = player.IsRunning;
        dbPlayer.Rights = player.Rights;
        dbPlayer.Gender = player.Appearance.Gender;
        dbPlayer.LookJson = JsonSerializer.Serialize(player.Appearance.Look);
        dbPlayer.ColorsJson = JsonSerializer.Serialize(player.Appearance.Colors);

        foreach (var dbSkill in dbPlayer.Skills)
        {
            dbSkill.Level = player.Skills.GetLevel(dbSkill.SkillId);
            dbSkill.Experience = player.Skills.GetExperience(dbSkill.SkillId);
        }

        _db.Items.RemoveRange(dbPlayer.Items);
        SaveContainer(dbPlayer.Id, player.Inventory, ItemContainerType.Inventory);
        SaveContainer(dbPlayer.Id, player.Equipment, ItemContainerType.Equipment);
        SaveContainer(dbPlayer.Id, player.Bank, ItemContainerType.Bank);

        // Save friends and ignores
        _db.Friends.RemoveRange(dbPlayer.Friends);
        foreach (var friendLong in player.FriendsList)
        {
            _db.Friends.Add(new DbFriend
            {
                PlayerId = dbPlayer.Id,
                FriendNameLong = friendLong
            });
        }
        _db.Ignores.RemoveRange(dbPlayer.Ignores);
        foreach (var ignoreLong in player.IgnoreList)
        {
            _db.Ignores.Add(new DbIgnore
            {
                PlayerId = dbPlayer.Id,
                IgnoreNameLong = ignoreLong
            });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Saved player: {Username}", player.Username);
    }

    private void SaveContainer(int playerId, ItemContainer container, ItemContainerType type)
    {
        for (int i = 0; i < container.Capacity; i++)
        {
            var item = container.Get(i);
            if (item is null) continue;
            _db.Items.Add(new DbItem
            {
                PlayerId = playerId,
                ContainerType = type,
                Slot = i,
                ItemId = item.Id,
                Amount = item.Amount
            });
        }
    }

    private static int[]? DeserializeIntArray(string json)
    {
        try { return JsonSerializer.Deserialize<int[]>(json); }
        catch { return null; }
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
