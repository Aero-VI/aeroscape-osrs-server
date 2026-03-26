# AeroScape Gap Analysis — Legacy Java (DavidScape 508) vs C# Port

> **Generated:** 2026-03-26  
> **Method:** Exhaustive file-by-file comparison of `legacy-java/server508` (40,377 lines, 87 Java files) against `AeroScape.Server.*` (12,819 lines, ~60 C# source files excluding obj/).  
> **Cross-referenced against:** `FEATURE_LIST.md`

---

## Executive Summary

The C# port implements approximately **25–30%** of the legacy Java server's functionality. The networking skeleton, basic entity model, login/JS5 handshake, walking, basic melee combat vs NPCs, and a handful of skills have been ported. However, the vast majority of game content — shops, quests, minigames, PvP combat, the magic system runtime, the full equipment system, NPC dialogues, object interactions, and virtually all "fun" gameplay — exists only as **stub handlers with `// TODO` comments** or is completely absent.

### Lines of Code Comparison
| Layer | Java (lines) | C# (lines) | Ratio |
|-------|-------------|------------|-------|
| Player entity + state | 5,148 | 190 | 3.7% |
| ActionButtons routing | 2,769 | 191 | 6.9% |
| Magic system | 2,346 + 1,212 + 538 = 4,096 | 195 (data only) | 4.8% |
| ObjectOption1 (world interactions) | 1,866 | 269 (all interactions) | 14.4% |
| Equipment | 1,453 | ~80 (equip handler) | 5.5% |
| Commands | 1,089 | 329 | 30.2% |
| Smithing | 1,978 | 155 | 7.8% |
| Shops | 1,062 | 0 | 0% |
| NPC dialogues (NPCOption1/2/3) | 1,375 | 0 | 0% |
| File save/load | 1,142 | 203 (EF Core) | 17.8% |
| Frames (outgoing packets) | 1,147 | 185 | 16.1% |
| Total | 40,377 | 12,819 | 31.7% |

---

## 1. Missing Networking & Protocol Features

### 1.1 Outgoing Packets — Mostly Missing

The legacy `Frames.java` (1,147 lines) implements **40+ outgoing packet types**. The C# `PacketSender.cs` implements only **12**:

| Outgoing Packet | Java (Frames.java) | C# (PacketSender.cs) | Status |
|----------------|--------------------|--------------------|--------|
| SendMessage | ✅ | ✅ | Ported |
| MapRegion | ✅ | ✅ | Ported |
| SetItems (inventory) | ✅ | ✅ | Ported |
| SetItems (equipment) | ✅ | ✅ | Ported |
| SendSkill | ✅ | ✅ | Ported |
| SendEnergy | ✅ | ✅ | Ported |
| SendWeight | ✅ | ⚠️ | Stub (always sends 0) |
| SendConfig | ✅ | ✅ | Ported |
| SetSidebar | ✅ | ✅ | Ported |
| SetInterface | ✅ | ✅ | Ported |
| Logout | ✅ | ✅ | Ported |
| SystemUpdate | ✅ | ✅ | Ported |
| SetPlayerOption | ✅ | ✅ | Ported |
| **createObject** | ✅ | ❌ | **Missing** — no object spawn/removal packets |
| **createLocalObject** | ✅ | ❌ | **Missing** |
| **deleteLocalObject** | ✅ | ❌ | **Missing** |
| **createGlobalObject** | ✅ | ❌ | **Missing** |
| **createProjectile** | ✅ | ❌ | **Missing** — no projectile rendering |
| **createGlobalProjectile** | ✅ | ❌ | **Missing** |
| **playSound** | ✅ | ❌ | **Missing** — no sound support |
| **sendClanChat** | ✅ | ❌ | **Missing** |
| **sendSentPrivateMessage** | ✅ | ❌ | **Missing** — PM sending side |
| **sendReceivedPrivateMessage** | ✅ | ❌ | **Missing** — PM receiving side |
| **sendFriend** | ✅ | ❌ | **Missing** — friend online status |
| **sendIgnores** | ✅ | ❌ | **Missing** |
| **sendMapRegion2** | ✅ | ❌ | **Missing** — construction instanced region |
| **setWindowPane** | ✅ | ❌ | **Missing** — HD/LD client pane switching |
| **setNPCId** | ✅ | ❌ | **Missing** — NPC head on dialogue |
| **animateInterfaceId** | ✅ | ❌ | **Missing** — interface animation |
| **setConfig1/setConfig2** | ✅ | ❌ | **Missing** — 1-byte/2-byte config variants |
| **runScript** | ✅ | ❌ | **Missing** — CS2 script execution (input boxes) |
| **setBankOptions** | ✅ | ❌ | **Missing** — bank access masks |
| **setAccessMask** | ✅ | ❌ | **Missing** — interface interaction permissions |
| **setTab** | ✅ | ❌ | **Missing** — individual tab assignment |
| **itemOnInterface** | ✅ | ❌ | **Missing** — display item on interface |
| **setString** | ✅ | ❌ | **Missing** — set text on interface |
| **setOverlay** | ✅ | ❌ | **Missing** — overlay interfaces |
| **sendPlayerCoords** | ✅ | ❌ | **Missing** — minimap flag/coordinate hint |
| **restoreTabs** | ✅ | ❌ | **Missing** — full tab sidebar restoration |
| **teleportOnMapdata** | ✅ | ❌ | **Missing** — teleport with custom map |
| **setLoot** | ✅ | ❌ | **Missing** — ground item display to client |
| **connecttofserver** | ✅ | ❌ | **Missing** — friend server connect |

**Impact:** Without object creation/deletion packets, projectile packets, interface text packets, runScript, and access masks, most game content simply cannot function even if the server-side logic were ported.

### 1.2 Incoming Packet Handling

Decoders exist for most incoming packet types, but many handlers are **empty stubs**:

| Incoming Packet | Java Handler | C# Decoder | C# Handler Logic | Status |
|----------------|-------------|-----------|-----------------|--------|
| Walking | ✅ Full | ✅ | ✅ Working | Complete |
| PublicChat | ✅ Full | ✅ | ✅ Working | Complete |
| Commands | ✅ 1,089 lines | ✅ | ⚠️ ~20 commands (vs 60+) | Partial |
| ActionButtons | ✅ 2,769 lines | ✅ | ⚠️ All TODOs | **Stub** |
| Equipment | ✅ 1,453 lines | ✅ | ⚠️ Basic equip only | Partial |
| DropItem | ✅ Full | ✅ | ✅ Working | Complete |
| PickupItem | ✅ Full | ✅ | ✅ Working | Complete |
| ItemOnItem | ✅ 385 lines | ✅ | ❌ Stub | **Missing** |
| ItemOnObject | ✅ 431 lines | ✅ | ❌ Stub | **Missing** |
| ItemOnNPC | ✅ 34 lines | ✅ | ❌ Stub | **Missing** |
| ItemOption1 | ✅ 559 lines (food/potions) | ✅ | ❌ Stub | **Missing** |
| ItemOption2 | ✅ 50 lines | ✅ | ❌ Stub | **Missing** |
| ItemSelect | ✅ 1,145 lines (bones, etc.) | ✅ | ❌ Stub | **Missing** |
| ItemOperate | ✅ 72 lines (glory, DFS) | ✅ | ❌ Stub | **Missing** |
| ObjectOption1 | ✅ 1,866 lines | ✅ | ⚠️ Banks + ladders only | **Mostly missing** |
| ObjectOption2 | ✅ 89 lines | ✅ | ❌ Stub | **Missing** |
| NPCOption1 | ✅ 978 lines | ✅ | ⚠️ Attack only | **Mostly missing** |
| NPCOption2 | ✅ 305 lines | ✅ | ❌ Stub | **Missing** |
| NPCOption3 | ✅ 92 lines | ✅ | ❌ Stub | **Missing** |
| NPCAttack | ✅ Full | ✅ | ✅ Working | Complete |
| MagicOnNPC | ✅ 1,212 lines | ✅ | ❌ Stub | **Missing** |
| MagicOnPlayer | ✅ 91 lines | ✅ | ❌ Stub | **Missing** |
| PlayerOption1 | ✅ 72 lines (follow) | ✅ | ⚠️ Basic follow | Partial |
| PlayerOption2 | ✅ 66 lines (trade) | ✅ | ❌ Stub | **Missing** |
| PlayerOption3 | ✅ 129 lines (duel) | ✅ | ❌ Stub | **Missing** |
| Prayer | ✅ 268 lines | ✅ | ✅ Data ported | ⚠️ Needs config send |
| SwitchItems | ✅ Full | ✅ | ❌ Stub | **Missing** |
| SwitchItems2 | ✅ Full | ✅ | ❌ Stub | **Missing** |
| Assault | ✅ 402 lines | ✅ | ❌ Stub | **Missing** |
| BountyHunter | ✅ 74 lines | ✅ | ❌ Stub | **Missing** |
| ClanChat | ✅ 15 lines | ✅ | ❌ Stub | **Missing** |
| ItemGive | ✅ 39 lines | ✅ | ❌ Stub | **Missing** |

### 1.3 Connection & Security — Missing

| Feature | Java | C# | Status |
|---------|------|-----|--------|
| Connection flood protection (Protect.java) | ✅ 292 lines | ❌ | **Missing** |
| IP ban system | ✅ File-based | ❌ | **Missing** |
| IP mute system | ✅ File-based | ❌ | **Missing** |
| Character ban system | ✅ File-based | ❌ | **Missing** |
| Max connections per host | ✅ | ❌ | **Missing** |
| Username pattern detection | ✅ | ❌ | **Missing** |
| HD/LD client detection | ✅ Window pane 746/548 | ❌ | **Missing** |
| Account verification code | ✅ | ❌ | **Missing** |
| Idle timeout (5 ticks) | ✅ | ❌ | **Missing** |
| Profanity filter | ✅ | ❌ | **Missing** |

---

## 2. Missing Combat Mechanics

### 2.1 Player vs Player (PvP) — Completely Missing

The legacy `PlayerCombat.java` (488 lines) handles full PvP. **None of this exists in C#.**

- ❌ Player attacking another player (melee)
- ❌ PvP max hit formula with equipment bonuses
- ❌ Skull system (180-tick skull timer)
- ❌ Skull head icon rendering
- ❌ Wilderness level calculation (`(absY - 3520) + 1`)
- ❌ Wilderness combat level range enforcement
- ❌ Freeze mechanic (prevents movement)
- ❌ PvP death — items dropped for killer
- ❌ PvE death — gravestone system (object 12719, 200-tick timer, item recovery)
- ❌ Death animation + respawn cycle
- ❌ Retribution prayer AoE damage on death
- ❌ Protection prayer effectiveness (partial bypass)
- ❌ Auto-retaliate against players

### 2.2 Special Attacks — Completely Missing

- ❌ Dragon Battleaxe special (strength boost + forced chat, 100% energy)
- ❌ Dragon Claws special (4-hit decreasing damage)
- ❌ Dragonfire Shield ranged attack (50 max, 10-tick cooldown)
- ❌ Godsword specials (AGS, BGS, SGS, ZGS)
- ❌ Special attack bar regeneration (1% per 2 ticks)
- ❌ Special attack energy tracking (config 300/301)
- ❌ Special attack toggle on weapon interfaces

### 2.3 Ranged Combat — Completely Missing

Legacy `PlayerCombat.java` and `PlayerNPCCombat.java` handle ranged:

- ❌ Bow/crossbow attack handling
- ❌ Arrow/bolt consumption
- ❌ Ranged max hit formula
- ❌ Ranged projectile rendering (GFX + projectile packets)
- ❌ Arrow ID → projectile graphic mapping (data exists in C# `CombatSystem` but unused)
- ❌ Dark bow special
- ❌ Crystal bow
- ❌ Karil's crossbow
- ❌ Dart/knife/thrown weapon handling

### 2.4 Melee Combat — Half-Baked

The C# `CombatSystem.cs` implements basic player→NPC melee but is incomplete:

- ✅ Max hit formula (ported correctly from `PlayerNPCCombat.maxMeleeHit()`)
- ✅ Basic accuracy roll
- ✅ NPC death + respawn queue
- ✅ HP XP on kill
- ⚠️ No individual attack timers per player (uses "even/odd tick" hack)
- ⚠️ No weapon-specific attack speed
- ⚠️ No weapon-specific animations (always 422/punch)
- ⚠️ No attack style XP distribution in combat tick (data exists in `AwardCombatXp` but not called from `ProcessTick`)
- ❌ No equipment bonus calculation for accuracy
- ❌ No weapon interface switching (`PlayerWeapon.java` — 316 lines mapping weapon → interface)
- ❌ No attack delay per weapon type

### 2.5 NPC Combat AI — Half-Baked

`NpcCombatAi.cs` covers basics but misses:

- ✅ NPC → player melee attacks
- ✅ Dragon fire special attack check
- ✅ Anti-dragon shield check
- ✅ Protect from Melee prayer check
- ⚠️ No NPC-specific attack animations (only hardcoded for a few IDs)
- ⚠️ No NPC-specific max hits from config data
- ❌ No NPC aggression (NPCs don't initiate combat)
- ❌ No NPC following/pathfinding toward players
- ❌ No multi-combat areas
- ❌ No NPC ranged/magic attacks
- ❌ No second hit (hit2) support for NPCs
- ❌ No NPC loot drops on death (`npcdrops.cfg`)
- ❌ No slayer task kill count tracking

---

## 3. Missing Magic System

The legacy magic system spans **4,096 lines** across `Magic.java`, `MagicNPC.java`, and `MagicOnPlayer.java`. The C# `MagicSystem.cs` (195 lines) contains **only static data tables** — level requirements, XP, max hits, rune costs, GFX IDs — but **no runtime execution logic**.

### 3.1 Modern Spellbook — Data Only, No Execution

- ✅ Spell ID mapping (button → spell)
- ✅ Level requirements per spell
- ✅ XP per spell
- ✅ Max hit per spell
- ✅ Rune requirements with staff reduction
- ✅ Caster/victim GFX IDs
- ❌ **No actual spell casting logic** — rune check + consume + damage + GFX + XP never runs
- ❌ No projectile creation for combat spells
- ❌ No magic accuracy formula
- ❌ No magic defence formula

### 3.2 Teleport Spells — Missing

- ❌ Varrock, Lumbridge, Falador, Camelot, Ardougne, Watchtower, Trollheim, Ape Atoll teleports
- ❌ Teleport animation + GFX sequence
- ❌ Rune consumption for teleports
- ❌ Wilderness teleport blocking (above level 20)

### 3.3 Utility Spells — Missing

- ❌ Low/High Alchemy (item → coins)
- ❌ Superheat Item (ore → bar, smithing level check)
- ❌ Bones to Peaches
- ❌ Charge spell (arena spell power boost)

### 3.4 Enchantment Spells — Missing

- ❌ All 6 enchantment levels (sapphire through onyx)
- ❌ Ring/bracelet/amulet/necklace enchanting
- ❌ Ring of recoil, Ring of dueling, Ring of life, etc.

### 3.5 Ancient Magicks — Data Only

- ✅ Level requirements, XP, max hits stored in arrays
- ❌ No Ice freeze implementation
- ❌ No Blood heal implementation
- ❌ No Shadow stat drain
- ❌ No Smoke poison
- ❌ No multi-target barrage (3-tile AoE)
- ❌ No ancient spell casting logic

### 3.6 Lunar Spellbook — Missing

- ❌ Vengeance spell (flag exists on Player but no implementation)
- ❌ All other lunar spells

### 3.7 Autocasting — Missing

- ❌ Autocast spell selection (interface 319)
- ❌ Autocast integration with combat loop

---

## 4. Missing Skills

### 4.1 Skills with Service Files but Incomplete Implementation

| Skill | Java Lines | C# Lines | Data Ported | Runtime Logic | Integration |
|-------|-----------|---------|------------|--------------|-------------|
| Mining | 401 | 170 | ✅ Rock→ore maps, XP, levels | ⚠️ State machine exists | ❌ Not wired to ObjectOption1 |
| Woodcutting | 355 | 166 | ✅ Tree→log maps, axe tiers | ⚠️ State machine exists | ❌ Not wired to ObjectOption1 |
| Fishing | 47 | 70 | ✅ Fish types, tools | ⚠️ State machine exists | ❌ Not wired to NPCOption1 |
| Smithing | 1,978 | 155 | ⚠️ Partial bar/item maps | ❌ No smelting dialogue, no anvil interface | ❌ Not wired |
| Construction | 342 | 100 | ⚠️ Room data only | ❌ No building, no POH instancing | ❌ Not wired |

**None of these skills actually work in-game** because they aren't connected to the packet handlers. The ObjectOption1 handler only handles banks and ladders; the NPCOption1 handler only handles combat.

### 4.2 Skills Completely Missing from C#

| Skill | Java Source | C# | Status |
|-------|-----------|-----|--------|
| **Cooking** | In `ObjectOption1.java` + `ActionButtons.java` | ❌ | No service, no logic |
| **Fletching** | In `ItemOnItem.java` (knife + logs) | ❌ | No service, no logic |
| **Firemaking** | In `ItemOnItem.java` (tinderbox + logs) | ❌ | No service, no fire objects |
| **Crafting** | In `ItemOnItem.java` (chisel + gems) + `ActionButtons.java` | ❌ | No service, no logic |
| **Herblore** | In `NPCOption1.java` (Kaqemeex) | ❌ | No service, no logic |
| **Agility** | In `ObjectOption1.java` (Barbarian course) | ❌ | No course, no obstacles |
| **Thieving** | In `NPCOption2.java` (pickpocketing) | ❌ | No service, no logic |
| **Slayer** | In `NPCOption1.java` (Duradel) | ❌ | No task system, no tracking |
| **Farming** | In `ObjectOption1.java` (patches) + `ItemOnObject.java` | ❌ | No growth timers, no patches |
| **Runecrafting** | In `ObjectOption1.java` (altars) + `ActionButtons.java` | ❌ | No altar logic, no rune multiplier |
| **Hunter** | In `NPCOption1.java` (implings) | ❌ | No catching, no jar looting |
| **Summoning** | In `ActionButtons.java` + `NPCOption1.java` | ❌ | No familiars, no pouches |
| **Prayer** (bone burying) | In `ItemSelect.java` (1,145 lines of bone types) | ❌ | Prayer toggle works but burying missing |

### 4.3 Food & Potions — Completely Missing

Legacy `ItemOption1.java` (559 lines) handles all food eating and potion drinking:

- ❌ No food items (shrimps through manta ray — 15+ food types)
- ❌ No eat animation / 3-tick delay
- ❌ No HP restoration
- ❌ No potion system (super restore, sara brew, stat potions — 10+ potion types with 4-dose systems)
- ❌ No drink animation / 3-tick delay
- ❌ No stat boosting/restoration formulas
- ❌ No empty vial creation after drinking

---

## 5. Missing Minigames & Activities

Every single minigame is **completely absent** from the C# port:

### 5.1 Castle Wars — Missing (Java: `CastleWarsFL.java` + `ObjectOption1.java` + `Player.java` + `Engine.java`)
- ❌ Team assignment (Saradomin/Zamorak)
- ❌ Waiting rooms with balancing
- ❌ Flag capture mechanics
- ❌ CW-specific items (barricades, explosives, bandages)
- ❌ Scoreboard
- ❌ Equipment restrictions
- ❌ CW death → team respawn
- ❌ Timed rounds with rewards

### 5.2 Fight Pits — Missing (Java: `Player.java` area checks + `PlayerCombat.java`)
- ❌ Waiting room + countdown
- ❌ Free-for-all PvP arena
- ❌ Last man standing detection
- ❌ Winner rewards (3rd age armour)

### 5.3 Barbarian Assault — Missing (Java: `Assault.java` — 402 lines)
- ❌ 5-wave system with NPC scaling
- ❌ 4 NPC types per wave
- ❌ Wave progression
- ❌ Height-based instancing
- ❌ Reward points system

### 5.4 Bounty Hunter — Missing (Java: `bountyHunter.java` — 74 lines)
- ❌ Crater area
- ❌ Opponent matching
- ❌ Target tracking interface

### 5.5 Clan Wars — Missing (Java: `Player.java` clan battle state)
- ❌ Challenge system
- ❌ Height-instanced arena
- ❌ Barrier countdown
- ❌ Win condition detection

### 5.6 Dueling — Missing (Java: `PlayerOption3.java` + `Player.java`)
- ❌ Challenge system
- ❌ 3-second countdown with forced chat
- ❌ Duel arena teleport
- ❌ Victory broadcast

### 5.7 Barrows — Missing (Java: `ObjectOption1.java` + `Player.java`)
- ❌ Brother tracking (boolean array exists in C# Player but unused)
- ❌ Chest reward system
- ❌ Barrows loot table

### 5.8 God Wars Dungeon — Missing (Java: `ObjectOption1.java` + `Player.java`)
- ❌ Kill count system (20 KC per faction)
- ❌ Faction lair doors
- ❌ GWD altars
- ❌ Boss room entry

### 5.9 Party Room — Missing (Java: `ObjectOption1.java` + `ShopHandler.java`)
- ❌ Item deposit chest
- ❌ Party drop lever + countdown
- ❌ Balloon drop mechanics

---

## 6. Missing Player Interaction Systems

### 6.1 Trading — Stub Only

`TradeManager.cs` has request matching logic but:

- ✅ Trade request matching (mutual request detection)
- ❌ No trade interfaces opened (335/334)
- ❌ No item offer/remove (1/5/10/All/X)
- ❌ No confirmation screen
- ❌ No item transfer on accept
- ❌ No decline handling with item return
- ❌ No free slot display
- ❌ No admin trade restriction

Legacy trade system: `PTrade.java` (383 lines) + `TButtons.java` (87 lines) + `TItem.java` (44 lines) + `PlayerTrade.java` (264 lines) = **778 lines** total.

### 6.2 Following — Partial

- ✅ Follow target index stored
- ❌ No actual pathfinding toward target
- ❌ No distance cap (>12 tiles = stop)
- ❌ No face-target during follow

### 6.3 Friends & Ignores — Stub Only

- ✅ Lists stored on Player entity and persisted to DB
- ❌ No online status notification to friends on login/logout
- ❌ No private message sending/receiving packets
- ❌ No ignore list enforcement (blocked PMs)

### 6.4 Clan Chat — Completely Missing

Legacy: `ClanMain.java` (363 lines) + `ClanList.java` (336 lines) + supporting files = **737+ lines**

- ❌ No clan channel creation/naming
- ❌ No join/leave
- ❌ No clan message routing
- ❌ No rank system
- ❌ No kick/ban from clan
- ❌ No LootShare

---

## 7. Missing Item Systems

### 7.1 Banking — Stub Only

`BankHandler.cs` opens the bank interface, but:

- ✅ Bank opening (object detection for bank booths)
- ❌ No deposit (1/5/10/All/X)
- ❌ No withdraw (1/5/10/All/X)
- ❌ No bank tabs (9 tabs + main)
- ❌ No insert/swap mode
- ❌ No withdraw-as-note
- ❌ No bank search
- ❌ No setBankOptions access masks

Legacy: `PlayerBank.java` (356 lines) + `BankUtils.java` (45 lines)

### 7.2 Shops — Completely Missing

Legacy `ShopHandler.java` (1,062 lines) defines **18+ shops** with buy/sell mechanics:

- ❌ No shop system whatsoever
- ❌ No shop NPCs
- ❌ No buy/sell interface
- ❌ No price calculation
- ❌ No stock management / restocking
- ❌ No General Store sell-anything mechanic

### 7.3 Equipment System — Partial

`EquipmentHandler.cs` handles basic equip, but:

- ✅ Basic equip/unequip
- ⚠️ Equipment bonus calculation exists in skeleton but untested
- ❌ No name-based slot detection (legacy Equipment.java has extensive name→slot mapping)
- ❌ No level requirement enforcement (Attack, Defence, Strength, Magic, Ranged, etc.)
- ❌ No two-handed weapon detection (data mapping missing)
- ❌ No full/half body/mask detection
- ❌ No equipment stats interface (667)
- ❌ No skillcape level 120 requirement
- ❌ No Dragon Slayer quest gate for rune platebody
- ❌ No weapon interface switching (16+ combat tab variants per weapon type)

Legacy: `Equipment.java` (1,453 lines) + `PlayerWeapon.java` (316 lines) = **1,769 lines**

### 7.4 Item Interactions — Missing

- ❌ Item on Item (gem cutting, godsword assembly, fletching, firemaking — `ItemOnItem.java` 385 lines)
- ❌ Item on Object (ore on furnace, bar on anvil, fish on range — `ItemOnObject.java` 431 lines)
- ❌ Item on NPC (`ItemOnNPC.java` 34 lines)
- ❌ Item Operate (glory teleports, DFS special — `ItemOperate.java` 72 lines)
- ❌ Item consuming (food, potions, bones — `ItemOption1.java` 559 lines + `ItemSelect.java` 1,145 lines)
- ❌ Noted item detection/handling
- ❌ Destroy item confirmation for untradeables
- ❌ Inventory item switching/rotating (SwitchItems handlers are stubs)

### 7.5 Ground Items — Partial

- ✅ Drop + pickup logic
- ✅ Tick-based despawn timer (240 ticks)
- ✅ Private→public visibility transition
- ❌ No `setLoot` packet — client never actually sees ground items rendered
- ❌ No untradeable-only-visible-to-owner logic
- ❌ No stacking of same items at same position (logic exists in service but untested)

---

## 8. Missing World & Object Interactions

### 8.1 Object Interactions — Almost Entirely Missing

Legacy `ObjectOption1.java` (1,866 lines) + `ObjectOption2.java` (89 lines) handle:

The C# `ObjectInteractHandler` only handles **banks and ladders/stairs**. Everything else returns "Nothing interesting happens."

Missing object interactions:
- ❌ All 30+ tree objects (Woodcutting)
- ❌ All 25+ rock objects (Mining)
- ❌ All 12 Runecrafting altars
- ❌ Prayer altars (restore prayer)
- ❌ All doors/gates (coordinate-based door handling)
- ❌ Agility obstacles (rope swings, log balances, nets, walls)
- ❌ GWD objects (rope entry, faction doors, ice bridge, altars)
- ❌ Castle Wars objects (flags, barriers, supply tables)
- ❌ Barbarian Assault wave entrances
- ❌ All portals (PK, Clan Wars, Fight Pits, Bounty Hunter)
- ❌ Wilderness ditch (jump + animation)
- ❌ Gravestone (item recovery)
- ❌ Barrows chest (reward claiming)
- ❌ Party room lever + chest
- ❌ Ancient altar (spellbook switch to/from Ancients)
- ❌ Lunar altar (spellbook switch to/from Lunars)
- ❌ Farming patches
- ❌ Furnace (smelting trigger)
- ❌ Anvil (smithing trigger)
- ❌ Cooking range/fire
- ❌ Cannon base placement
- ❌ Essence mining rock (object 16687)
- ❌ House portal (object 15482)
- ❌ Highscores board (object 3192)

### 8.2 Object Spawning — Missing

- ❌ No `createObject` / `deleteLocalObject` / `createGlobalObject` packets
- ❌ No object spawn system (fires from firemaking, doors opening/closing)
- ❌ No object replacement (tree stump after cutting, depleted rock)
- ❌ No object respawn timers

---

## 9. Missing NPC Systems

### 9.1 NPC Dialogues — Completely Missing

Legacy `NPCOption1.java` (978 lines) + `NPCOption2.java` (305 lines) + `NPCOption3.java` (92 lines) + `PacketManager.java` (1,175 lines) handle extensive NPC conversations:

- ❌ No chatbox dialogue interfaces (241, 458)
- ❌ No multi-option dialogues
- ❌ No NPC head model display in dialogue
- ❌ No animated NPC expressions
- ❌ No skill cape purchase dialogues (every skill tutor)
- ❌ No quest dialogues (Dragon Slayer branching conversations)
- ❌ No shop NPC → shop opening
- ❌ No skill tutor supply distribution

### 9.2 NPC Drops — Missing

- ❌ No drop table system (`npcdrops.cfg`)
- ❌ No random drop chances with min/max amounts
- ❌ No loot appearing on NPC death

### 9.3 NPC Spawning — Partial

- ✅ NPC spawn from JSON config
- ✅ NPC random walking within radius
- ⚠️ No NPC stats from config (combat level, max hit, attack type, weakness)
- ❌ No NPC aggression system (NPCs don't attack players proactively)
- ❌ No summoned NPC system (familiars, pets)

---

## 10. Missing Commands

The C# `CommandHandler.cs` implements **~17 commands**. The legacy `Commands.java` (1,089 lines) implements **60+ commands**:

### Present in C#:
`tele`, `item`, `master`, `pos`, `players`, `npc`, `anim`, `gfx`, `bank`, `empty`, `heal`, `setlevel`, `energy`, `yell`, `kick`, `teleto`, `teletome`, `update`, `interface`, `config`, `rights`

### Missing from C# (player commands):
- ❌ `::home` (teleport to Lumbridge)
- ❌ `::wildy` / `::pvp` (wilderness teleport)
- ❌ `::gwd` (God Wars teleport)
- ❌ `::house` (POH teleport)
- ❌ `::enter [name]` (enter someone's house)
- ❌ `::lock` / `::unlock` (house locking)
- ❌ `::party` (party room)
- ❌ `::assault` (Barbarian Assault)
- ❌ `::cw` (Castle Wars)
- ❌ `::kc` (GWD kill counts)
- ❌ `::commands` (help list)
- ❌ `::changepass`
- ❌ `::char` (character design screen)
- ❌ `::male` / `::female` (quick gender change)
- ❌ `::afk` / `::back` (AFK status toggle)
- ❌ `::smoke` (emote)
- ❌ `::whereis [name]`
- ❌ `::savebackup`
- ❌ `::reportbug` / `::reportabuse`
- ❌ `::joinchat` / `::leavechat` / `::c` (clan chat)
- ❌ `::newname` (rename clan)
- ❌ `::newroom` / `::deleteroom` (construction)
- ❌ `::verifycode`
- ❌ `::goinhouse [name]`

### Missing from C# (mod/admin commands):
- ❌ `::jail` / `::mute` / `::unmute` / `::ban` / `::unban`
- ❌ `::staff` / `::god2` / `::godoff` / `::private`
- ❌ `::pnpc [id]` / `::unpc` (transform into NPC)
- ❌ `::object [id]` (spawn object)
- ❌ `::slave` (set all skills to 98)
- ❌ `::emote [id]` / `::si [id]`
- ❌ `::coords` (show coordinates)
- ❌ `::kill [name]` (instant kill)
- ❌ `::rs` (restore special)
- ❌ `::ancients` / `::modern` / `::lunar` (switch spellbook)
- ❌ `::fullkc` (set all GWD KC to 200)
- ❌ `::givemember` / `::removemember`
- ❌ `::ipban` / `::ipmute`
- ❌ `::clangame`
- ❌ `::rebuildnpclist`
- ❌ `::logout`

---

## 11. Missing Quest System

### 11.1 Dragon Slayer — Completely Missing

Legacy implements a **full 6-stage quest** across multiple files:

- ❌ Quest stage tracking (0–5)
- ❌ Guildmaster dialogue (NPC 198)
- ❌ Oziach dialogue (NPC 747) — kill dragon task
- ❌ Oracle (NPC 746) — gives map
- ❌ Duke of Lumbridge (NPC 741) — anti-dragon shield
- ❌ Klarense (NPC 744) — boat to Crandor
- ❌ Boat cutscene (multi-stage animation)
- ❌ Elvarg boss fight
- ❌ Quest completion rewards (QP, XP, item unlocks)
- ❌ Quest journal in quest tab
- ❌ Quest complete interface (277)

### 11.2 Quest Cape — Missing

- ❌ Wise Old Man (NPC 2253) quest cape award

---

## 12. Missing Miscellaneous Systems

### 12.1 Character Customization — Missing
- ❌ Character design screen (interface 771)
- ❌ Hairdresser NPC (598) — hair/beard interfaces
- ❌ Clothing shop (NPC Thessalia) — torso/legs customization

### 12.2 Emotes — Missing
- ❌ 44 standard emotes on emote tab (interface 464)
- ❌ Skillcape emotes (50+ unique animations + GFX)
- ❌ God cape emotes

### 12.3 Run Energy — Partial
- ✅ Energy value tracked
- ✅ Energy drain while running
- ⚠️ Energy regeneration logic (exists in `GameEngine` tick but unclear if correct rate)
- ❌ No minimap orb toggle sync

### 12.4 Stat Restoration — Missing
- ❌ Natural stat restore (every 75 ticks, boosted→normal)
- ❌ Potion stat decay

### 12.5 Random Events — Missing
- ❌ Anti-bot skill selection challenge (interface 134)
- ❌ Wrong answer = disconnect
- ❌ Correct answer = coin reward

### 12.6 Jailing System — Missing
- ❌ Jail location, timer, forced chat
- ❌ Command restriction while jailed

### 12.7 Membership System — Missing
- ❌ Member flag, member shop, member area

### 12.8 Login/Logout — Partial
- ✅ Login with auto-registration + SHA-256 hashing
- ❌ No login/logout broadcast messages
- ❌ No welcome messages with update notes
- ❌ No HD/LD client detection and window pane switching
- ❌ No tab restoration on login (`restoreTabs`)
- ❌ No player option setting on login ("Follow", "Trade", "Attack")
- ❌ No backup save system

### 12.9 Highscores — Missing
- ❌ No top-30 player tracking
- ❌ No highscores board object

### 12.10 Player-as-NPC — Missing
- ❌ `::pnpc` / `::unpc` transformation
- ❌ Gnomecopter item mechanics

### 12.11 Wilderness Mechanics — Missing
- ❌ Wilderness level display
- ❌ Combat level range HUD
- ❌ Attack option toggling (Walk Here ↔ Attack based on location)

### 12.12 Cannon System — Missing
- ❌ Cannon base placement
- ❌ Cannon assembly via item-on-object
- ❌ One-per-player restriction

### 12.13 Amulet of Glory Teleports — Missing
- ❌ Operate item → 3-destination dialogue

### 12.14 Skill Teleports — Missing
- ❌ Click skill → teleport to training area

### 12.15 Update Notes System — Missing
- ❌ Multi-page update notes on login

---

## 13. Half-Baked Ports (Exist but Don't Work)

These features have C# code that looks correct in isolation but **cannot function** due to missing infrastructure:

| Feature | What Exists | What's Missing to Make It Work |
|---------|------------|-------------------------------|
| Prayer system | Full toggle + conflict logic | `SendConfig` packets not called (TODOs), prayer drain not processed in tick |
| Mining service | Full rock→ore mapping, axe tiers, speed calc | Not wired to ObjectOption1, no rock depletion objects |
| Woodcutting service | Full tree→log mapping, axe tiers | Not wired to ObjectOption1, no tree stump objects |
| Fishing service | Fish types, tools, state machine | Not wired to NPCOption1 |
| Smithing service | Bar/item ID maps, partial XP | No smelting dialogue, no anvil interface, not wired |
| Construction service | Room data array | No POH instancing, no building interface, not wired |
| Magic system | All data tables (levels, XP, runes, GFX) | No casting logic, no projectiles, not wired to MagicOnNPC |
| Combat XP distribution | `AwardCombatXp()` method with correct formulas | Never called from `ProcessTick()` |
| Ground item service | Drop/pickup/timer logic | No `setLoot` packet — items invisible to client |
| Trade manager | Request matching logic | No trade interfaces, no item transfer |
| Following | Target index stored | No pathfinding, no movement toward target |
| Friends/Ignores | Persisted to database | No online notifications, no PM packets |
| Equipment bonuses | 13-slot array on Player | Never calculated from item definitions |
| Barrows tracking | Boolean[6] on Player | Never checked, no brother combat, no chest |

---

## 14. Data Layer Assessment

### What's Persisted (EF Core):
- ✅ Player credentials (username, password hash)
- ✅ Position (X, Y, Z)
- ✅ Skills (25 skills with level + XP)
- ✅ Inventory items
- ✅ Equipment items
- ✅ Bank items
- ✅ Friends list
- ✅ Ignore list
- ✅ Player rights

### What's NOT Persisted (but should be per legacy):
- ❌ Quest progress (DragonSlayer stage)
- ❌ Slayer task + amount
- ❌ GWD kill counts
- ❌ Construction rooms/objects
- ❌ Familiar type
- ❌ Clan chat data
- ❌ Skull timer
- ❌ Special attack energy
- ❌ Member status
- ❌ Verification code
- ❌ Jail status
- ❌ Duel/CW/Assault/BH state
- ❌ House locked status
- ❌ Spellbook (modern/ancient/lunar)
- ❌ Cannon placed flag
- ❌ Highscores
- ❌ Backup saves

---

## 15. Engine & Tick Processing

### Present:
- ✅ 600ms game tick
- ✅ Player update cycle (appearance, animation, GFX, hit, forced chat, face entity/coordinate)
- ✅ NPC update cycle
- ✅ Movement processing (walk + run)
- ✅ Map region boundary detection

### Missing:
- ❌ No combat tick processing integration (CombatSystem.ProcessTick not confirmed called)
- ❌ No NPC aggression tick
- ❌ No prayer drain tick
- ❌ No stat restoration tick (every 75 ticks)
- ❌ No special attack regeneration tick
- ❌ No skull timer decay
- ❌ No ground item tick (despawn/visibility transitions)
- ❌ No shop restock tick
- ❌ No farming growth tick
- ❌ No fire despawn tick
- ❌ No gravestone decay tick
- ❌ No CW/assault/pits game timers
- ❌ No idle timeout processing
- ❌ No auto-save every 10 seconds
- ❌ No NPC follow → player movement tracking
- ❌ No summoned NPC (familiar) following

---

## 16. Priority Recommendations

### Tier 1 — Core Infrastructure (Blocks Everything Else)
1. **Outgoing packet completion** — createObject, projectile, runScript, setString, setAccessMask, setLoot, sendFriend, sendPrivateMessage
2. **ActionButtons routing** — connect the 191-line stub to actual subsystems
3. **ObjectOption1 routing** — connect ~40 object type handlers
4. **NPCOption1/2/3 routing** — connect shop, dialogue, skill tutor handlers
5. **ItemOption1 handler** — food/potions are fundamental to gameplay
6. **Ground item rendering** — `setLoot` packet so players can see dropped items

### Tier 2 — Core Gameplay
7. **Equipment system** — level requirements, weapon interfaces, bonus calculation
8. **Shop system** — essential for item economy
9. **Magic spell casting** — wire data tables to actual execution
10. **PvP combat** — skull, wilderness, death system
11. **Banking** — deposit/withdraw operations
12. **Trading** — wire TradeManager to interfaces

### Tier 3 — Content
13. Wire existing skill services (Mining, WC, Fishing) to packet handlers
14. Implement missing skills (Cooking, Fletching, Firemaking, Crafting, etc.)
15. NPC dialogue system
16. Quest system (Dragon Slayer)
17. Minigames (Castle Wars, Fight Pits, etc.)

---

*This analysis was generated by exhaustive comparison of 87 Java source files (40,377 lines) against 60 C# source files (12,819 lines). Every Java class was examined for features that should exist in the C# port.*
