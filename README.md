# AeroScape — RS2 508 Private Server in C# / .NET 10

A from-scratch RuneScape 2 (revision 508) private server built on modern .NET 10 with a clean layered architecture.

## Architecture

```
AeroScape.Server.App          — Generic Host entry point, Serilog, startup orchestration
AeroScape.Server.Network      — TCP listener, System.IO.Pipelines packet framing,
                                 ISAAC encryption, JS5 cache serving, player/NPC updating
AeroScape.Server.Core          — Protocol-agnostic game logic: entities, combat, movement,
                                 skills, ground items, trade system
AeroScape.Server.Data          — EF Core persistence (SQLite dev / SQL Server prod)
```

### Key Design Decisions

- **System.IO.Pipelines** for the game packet read loop — zero-copy buffering, pooled memory, automatic back-pressure, clean partial-packet handling
- **JSON-driven protocol** (`Protocol_508.json`) — no hardcoded opcodes; packet definitions loaded at startup
- **ISAAC PRNG** for opcode encryption matching the 508 client
- **Protocol-agnostic messages** — the Core layer operates on typed records (`WalkMessage`, `CommandMessage`, etc.), never raw bytes
- **DI-resolved handlers** — each message type has an `IMessageHandler<T>` resolved from the service container per scope
- **Entity update packets** — full player and NPC update cycle with movement, appearance, combat hits, animations, graphics, forced chat, face entity/coordinate

## Features

- ✅ Login with auto-registration & password hashing (SHA-256)
- ✅ JS5 cache serving (reads `main_file_cache.dat2` / idx files)
- ✅ Walking & running with run energy drain/restore
- ✅ Map region loading with boundary detection
- ✅ Public chat with packed text
- ✅ Private messaging between online players
- ✅ Friends list & ignore list (persisted)
- ✅ Full equipment system with 2H weapon/shield handling
- ✅ Inventory management (equip, drop, swap, pick up ground items)
- ✅ 25-skill system with XP, leveling, and combat level calculation
- ✅ Melee combat vs NPCs with accuracy rolls, max hit, death, and respawn
- ✅ NPC random walking within spawn radius
- ✅ Ground item spawning, pickup, expiry, and public visibility
- ✅ Admin commands: `::tele`, `::item`, `::master`, `::npc`, `::kick`, `::yell`, etc.
- ✅ Player/NPC update packets (appearance, animation, graphic, hit splats, forced chat)
- ✅ Sidebar interfaces, config packets, system update countdown
- ✅ Trade session framework
- ✅ EF Core persistence: position, skills, inventory, equipment, bank, friends, ignores

## Building

```bash
dotnet build
```

## Running

```bash
dotnet run --project src/AeroScape.Server.App
```

Listens on port **43594** by default. Connect with a 508 client.

### Cache Setup

Place your revision 508 cache files in one of:
- `<output>/cache/`
- `/home/cache/rev508/cache/`
- `~/.aeroscape/cache/`

Files needed: `main_file_cache.dat2`, `main_file_cache.idx0` through `idx255`.

## Configuration

Uses `appsettings.json` or environment variables:

```json
{
  "Database": {
    "Provider": "Sqlite"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=AeroScape.db"
  }
}
```

Set `Provider` to `SqlServer` for production with SQL Server.

## Project Structure

```
src/
├── AeroScape.Server.App/
│   ├── Program.cs              — Host builder, startup, data loading
│   └── npc_spawns.json         — NPC spawn definitions
├── AeroScape.Server.Core/
│   ├── Constants/              — Server constants, login codes
│   ├── Crypto/                 — ISAAC PRNG
│   ├── Entities/               — Player, NPC, Item, Position, SkillSet, etc.
│   ├── Game/                   — GameEngine, GameWorld, CombatSystem, etc.
│   ├── Interfaces/             — IMessageHandler, IPlayerRepository, IPlayerSession
│   ├── Messages/               — Protocol-agnostic game message records
│   └── Util/                   — Chat encoding
├── AeroScape.Server.Network/
│   ├── Handlers/               — Message handlers (walk, chat, commands, etc.)
│   ├── Js5/                    — JS5 cache serving
│   ├── Pipeline/               — ConnectionPipeline, PacketDispatcher
│   ├── Protocol/               — PacketReader, PacketBuilder, ProtocolService
│   ├── Session/                — PlayerSession, PlayerSessionManager
│   ├── Tcp/                    — TcpServerService (BackgroundService)
│   └── Updating/               — Player/NPC update packets, PacketSender
└── AeroScape.Server.Data/
    ├── Models/                 — EF Core entities
    ├── Repositories/           — EfPlayerRepository
    └── AeroScapeDbContext.cs   — Database context
```
