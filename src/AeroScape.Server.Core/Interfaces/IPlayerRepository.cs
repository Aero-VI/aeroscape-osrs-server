namespace AeroScape.Server.Core.Interfaces;

/// <summary>
/// Persistence abstraction — the core engine calls this to load/save player state.
/// Implemented by the Data layer (EF Core).
/// </summary>
public interface IPlayerRepository
{
    Task<Entities.Player?> LoadAsync(string username, CancellationToken ct = default);
    Task SaveAsync(Entities.Player player, CancellationToken ct = default);
    Task<bool> ExistsAsync(string username, CancellationToken ct = default);
    Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default);
    Task CreateAsync(string username, string password, CancellationToken ct = default);
}
