# AeroScape — Revision 508 RuneScape Private Server (.NET 10)

A modern, production-grade RSPS built from scratch in C# 12 / .NET 10.
No legacy Java paradigms — async/await, System.IO.Pipelines-ready,
DI-driven architecture, EF Core persistence, and zero hardcoded opcodes.

## Architecture

```
AeroScape.sln
├── AeroScape.Server.App        # Host, config, Program.cs
├── AeroScape.Server.Core       # Protocol-agnostic engine, entities, messages
├── AeroScape.Server.Network    # TCP listener, login pipeline, packet dispatch
└── AeroScape.Server.Data       # EF Core (SQLite dev / SQL Server prod)
```

### Key Design Decisions

- **Protocol Dictionary**: All opcodes/sizes loaded from `Protocol_508.json` — no magic numbers
- **Message Records**: Network layer decodes bytes → `record struct` messages. Engine has zero packet knowledge
- **Scoped Handlers**: Each packet dispatches to an `IMessageHandler<T>` resolved from DI scope
- **ISAAC Crypto**: Full ISAAC PRNG for opcode encryption/decryption
- **Dual DB Support**: SQLite for dev (works on Linux), SQL Server for production (one config swap)

## Quick Start

```bash
# Build
dotnet build

# Run (SQLite by default)
dotnet run --project src/AeroScape.Server.App

# Production (SQL Server)
ASPNETCORE_ENVIRONMENT=Production dotnet run --project src/AeroScape.Server.App
```

The server listens on **port 43594** by default.

## Database Configuration

### Development (default — SQLite)
```json
{
  "Database": { "Provider": "Sqlite" },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=AeroScape.db"
  }
}
```

### Production (SQL Server / LocalDB)
Set in `appsettings.Production.json`:
```json
{
  "Database": { "Provider": "SqlServer" },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=AeroScapeDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

For SQL Server Express:
```
Server=.\SQLEXPRESS;Database=RSPS_DB;Trusted_Connection=True;TrustServerCertificate=True;
```

## Protocol

The protocol definition lives in `Protocol_508.json`. To add new packets,
simply add entries to the `incoming` or `outgoing` sections and implement
the corresponding `IMessageHandler<T>`.

## Implemented Features

- ✅ TCP listener with async accept
- ✅ Full 508 login handshake with ISAAC cipher exchange
- ✅ JSON-driven protocol dictionary (no hardcoded opcodes)
- ✅ Walking/movement with run support
- ✅ Developer commands (::tele, ::item, ::master, ::pos)
- ✅ Appearance updates
- ✅ Button/interface handling (including logout)
- ✅ Equipment/inventory stubs
- ✅ Player persistence via EF Core (skills, items, position)
- ✅ Auto-registration on first login
- ✅ Sidebar interface setup
- ✅ Skill data sending
- ✅ Run energy tracking
- ✅ Game tick engine (600ms cycle)

## TODO

- [ ] Player updating (appearance block building)
- [ ] NPC updating
- [ ] JS5 cache serving
- [ ] Full equipment system with item definitions
- [ ] Combat system
- [ ] NPC spawning and interaction
- [ ] Object interaction
- [ ] Ground items
- [ ] Trade system
- [ ] Friends/ignore list
