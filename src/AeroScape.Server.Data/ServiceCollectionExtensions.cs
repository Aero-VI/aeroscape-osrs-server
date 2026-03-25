using AeroScape.Server.Core.Interfaces;
using AeroScape.Server.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AeroScape.Server.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAeroScapeData(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";

        services.AddDbContext<AeroScapeDbContext>(options =>
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                // Production: SQL Server / SQL Server Express / LocalDB
                var connStr = configuration.GetConnectionString("DefaultConnection")
                    ?? @"Server=(localdb)\mssqllocaldb;Database=AeroScapeDb;Trusted_Connection=True;MultipleActiveResultSets=true";
                options.UseSqlServer(connStr);
            }
            else
            {
                // Development: SQLite (works on Linux, macOS, Windows)
                var connStr = configuration.GetConnectionString("DefaultConnection")
                    ?? "Data Source=AeroScape.db";
                options.UseSqlite(connStr);
            }
        });

        services.AddScoped<IPlayerRepository, EfPlayerRepository>();

        return services;
    }
}
