using AeroScape.Server.Core.Game;
using AeroScape.Server.Data;
using AeroScape.Server.Network;
using AeroScape.Server.Network.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("AeroScape Server starting — Revision 508");

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog();

    // Core game engine
    builder.Services.AddSingleton<GameWorld>();
    builder.Services.AddSingleton<ItemDefinitionService>();
    builder.Services.AddSingleton<NpcSpawnLoader>();
    builder.Services.AddHostedService<GameEngine>();

    // Network layer
    builder.Services.AddAeroScapeNetwork();

    // Data layer (EF Core — SQLite dev / SQL Server prod)
    builder.Services.AddAeroScapeData(builder.Configuration);

    var app = builder.Build();

    // Run EF Core migrations on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AeroScapeDbContext>();
        await db.Database.EnsureCreatedAsync();
        Log.Information("Database initialized ({Provider})",
            builder.Configuration["Database:Provider"] ?? "Sqlite");
    }

    // Load protocol definition
    var protocol = app.Services.GetRequiredService<ProtocolService>();
    var protocolPath = Path.Combine(AppContext.BaseDirectory, "Protocol_508.json");
    if (!File.Exists(protocolPath))
    {
        // Try relative to source
        protocolPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "AeroScape.Server.Network", "Protocol", "Protocol_508.json");
    }
    await protocol.LoadAsync(protocolPath);

    // Load item definitions (optional JSON file)
    var itemDefs = app.Services.GetRequiredService<ItemDefinitionService>();
    var itemDefsPath = Path.Combine(AppContext.BaseDirectory, "item_definitions.json");
    if (File.Exists(itemDefsPath))
        await itemDefs.LoadFromFileAsync(itemDefsPath);

    // Load NPC spawns
    var npcLoader = app.Services.GetRequiredService<NpcSpawnLoader>();
    var npcSpawnPath = Path.Combine(AppContext.BaseDirectory, "npc_spawns.json");
    if (!File.Exists(npcSpawnPath))
    {
        npcSpawnPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "AeroScape.Server.App", "npc_spawns.json");
    }
    await npcLoader.LoadAsync(npcSpawnPath);

    Log.Information("AeroScape Server ready — listening on port 43594");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Server terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
