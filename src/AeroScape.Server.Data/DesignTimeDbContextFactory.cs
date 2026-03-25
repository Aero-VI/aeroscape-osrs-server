using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AeroScape.Server.Data;

/// <summary>
/// Design-time factory used by 'dotnet ef migrations' when the startup project
/// cannot build a host (e.g. the server tries to open sockets at startup).
/// Always uses SQLite so migrations work on any dev machine.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AeroScapeDbContext>
{
    public AeroScapeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AeroScapeDbContext>();
        optionsBuilder.UseSqlite("Data Source=AeroScape.db");
        return new AeroScapeDbContext(optionsBuilder.Options);
    }
}
