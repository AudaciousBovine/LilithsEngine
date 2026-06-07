# LilithsEngine — AI Agent Index

> **Agent-agnostic reference.** These docs are designed to be consumed by any AI coding agent (OpenCode, Claude, Codex, Kiro, Cursor, etc.). They describe the codebase structure, conventions, and data flow without assuming any particular tool or workflow.

A modular **V Rising** mod suite that allows server administrators to customize recipes, crafting stations, item names, tooltips, icons, stack sizes, and a wide range of server-side systems without directly editing game files.

## Active Modules

| Layer | Project | Role | Dependencies |
|-------|---------|------|-------------|
| **Mind** | `LilithsMind` | Shared library — pure C#, zero game dependencies | none |
| **Heart** | `LilithsHeart` | Server plugin — ECS access, module registration, sync sending | Mind |
| **Soul** | `LilithsSoul` | Client plugin — chat interception, UI panels, localization injection, client feature areas (Camera, CeilingTiles, Appearances) | Mind |
| **Cookbook** | `LilithsCookbook` | Server plugin — recipe, station, prisoner feed, and item function configuration | Heart + Mind |

## Planned Modules

These modules are designed but not yet implemented. Each is a standalone server-side child module of Heart unless noted. See `.aidevs/MODULES.md` for full design documentation.

Soul-internal client feature areas (Camera, CeilingTiles, Appearances) are documented separately in `.aidevs/SOUL_FEATURES.md`. These are not standalone modules — they are optional features built into LilithsSoul, enabled via config flags.

| Module | Role | Notes |
|--------|------|-------|
| **LilithsArmory** | Weapon and equipment stat configuration | Server. Parallel to Cookbook for gear. |
| **LilithsGrimoire** | Spell, buff, cooldown, and jewel trait configuration | Server + Soul patches. |
| **LilithsBounty** | Drop table configuration for enemies, resources, and chests | Server. |
| **LilithsArchitects** | Castle building recipe configuration and schematic placement | Server + Soul panel. Soft dep: Kindred Schematics. |
| **LilithsAdversaries** | Enemy and VBlood stat configuration, faction wanted system, NPC sieges | Server + Soul panel. |
| **LilithsWisdom** | Per-player conditional recipe and spell unlock gating | Server. |
| **LilithsTreasury** | Semantic stash system — player stash, castle stash, custom item variants, currencies | Server + Soul panel. |
| **LilithsBlessings** | Ritual sacrifice system — temporary and permanent buff application | Server + Soul panel. |
| **LilithsConquest** | Custom unit crafting, expeditions, simulated PvP battles, servant mission configuration | Server + Soul panel. |
| **LilithsMenagerie** | Creature capture, breeding, training, and production loops | Server + Soul panel. |
| **LilithsMachinations** | Quest system — daily/weekly/repeatable quests, multi-step chains, main quest modification | Server + Soul panel. |
| **LilithsNexus** | Teleportation — waygate network control, custom locations, personal waypoints, temporary portals | Server + Soul panel. |
| **LilithsExpansion** | Placeholder for future ideas | — |

## Key Files

| File | Purpose |
|------|---------|
| `.aidevs/ARCHITECTURE.md` | System architecture, layering, lifecycle, sync transport options |
| `.aidevs/MODULES.md` | Planned module designs, feature scope, and inter-module relationships |
| `.aidevs/CODE_MAP.md` | File-by-file index of all classes and responsibilities |
| `.aidevs/CONVENTIONS.md` | Design patterns, naming conventions, coding style |
| `.aidevs/DATA_FLOW.md` | Data flow diagrams, payload formats, lookup chains |
| `.aidevs/PREFAB_INDEX.md` | Prefab definition system reference |
| `.aidevs/GLOSSARY.md` | Domain-specific terminology |
| `.aidevs/SOUL_FEATURES.md` | Soul-internal client feature designs: Camera, CeilingTiles, Appearances |

## Tech Stack

- **Language:** C# 12.0, .NET 6.0
- **Mod Framework:** BepInEx 6 (IL2CPP)
- **Patcher:** HarmonyLib
- **ECS:** Unity Entities (DOTS) via V Rising assemblies
- **Server SDK:** VampireReferenceAssemblies v1.1.12
- **Client SDK:** VRising.Unhollowed.Client v1.1.9
- **Commands:** VampireCommandFramework v0.10.4
- **Serialization:** System.Text.Json

## External Mod Integrations (Soft Dependencies)

| Mod | Used By | Purpose |
|-----|---------|---------|
| Bloodcraft (mfoltz) | LilithsHeart | Inspiration source; soft compatibility desired for blood quality systems |
| Kindred Schematics | LilithsArchitects | Castle tile placement and schematic save/load functions |

## Lifecycle Overview

```
BepInEx Load
  ├─ HeartPlugin.Load()       — config, Harmony patches
  ├─ CookbookPlugin.Load()    — config, register generators, subscribe to Heart
  └─ SoulPlugin.Load()        — config, Harmony patches

World Ready (WarEventRegistrySystem)
  ├─ Heart.OnInitialize()
  │   ├─ PrefabNameResolver.Initialize()   — compiled defaults + alias overrides
  │   ├─ HeartConfigBuilder.RunIfRequested() — example/debug file generation
  │   ├─ ItemService.Initialize()          — loads Items/*.json → LilithItemConfig
  │   ├─ LocalizationService.Initialize()  — apply-layer diagnostic
  │   ├─ InterfaceService.Initialize()     — apply-layer diagnostic
  │   ├─ LocalizationFileService.Initialize() — loads Localization/<lang>/*.json
  │   ├─ Build baseline sync payload
  │   ├─ Fire OnInitialized → Cookbook applies ECS changes
  │   ├─ Rebuild payload with all overrides
  │   └─ Publish OnWorldReady
  └─ Soul.ClientInitPatch
      ├─ Build lookup tables (name→GUID, sprites, recipes)
      ├─ TryPreApplyCachedSync (from disk)
      ├─ TryPreApplyCachedLocalization (from disk)
      └─ Apply pending payloads if arrived early

Client Connects (ServerBootstrapSystem)
  └─ ClientConnectPatch → branches on SyncMode:
       ChunkPush  → SyncSender.EnqueueSyncTiers() (tiered chunks via chat)
       HttpServer → SyncSender.SendRedirect() ([[LG:sync-url:<url>:<fallback>]])
       StaticUrl  → SyncSender.SendRedirect() (configured static URL)

Client Chat Receive (ClientChatSystem)
  └─ ClientChatSystemPatch → SyncReceiver.TryHandleMessage()
      ├─ [[LG:sync-url:...]]    → SyncHttpFetcher.Fetch() (HTTP fetch)
      ├─ [[LG:lang-unavailable:...]] → log warning, stay on default language
      ├─ [[LG:begin:T:N:CKSUM]] → init tier accumulator
      ├─ [[LG:T:NNNN]]<data>    → accumulate chunk
      └─ [[LG:end:T:CKSUM]]     → verify, decompress, apply tier, cache to disk

Server Chat Receive (ServerBootstrapSystem)
  └─ ServerChatSystemPatch → handles Soul→Heart sentinels:
      ├─ [[LG:sync-fallback]]          → enqueue chunk delivery for that client
      ├─ [[LG:lang-request:X]]         → send localization payload for language X
      ├─ [[LG:appearance:update:...]]  → validate + store + broadcast appearance (AppearanceSyncEnabled)
      └─ [[LG:appearance:clear]]       → clear and broadcast player appearance removal
```

## How to Use These Docs

When an AI agent is asked to work on this codebase, it should first read the relevant `.aidevs/*.md` files to understand the architecture before making changes. The files are designed to be read independently:

| If you need... | Read this first |
|----------------|-----------------|
| Project overview, tech stack, lifecycle | `README.md` |
| System architecture, layer diagram, initialization order | `ARCHITECTURE.md` |
| Planned module designs and feature scope | `MODULES.md` |
| What every file does, class responsibilities | `CODE_MAP.md` |
| Design patterns, naming rules, coding style | `CONVENTIONS.md` |
| Data flow diagrams, payload formats, lookup chains | `DATA_FLOW.md` |
| Prefab definition system (item database) | `PREFAB_INDEX.md` |
| Domain terminology definitions | `GLOSSARY.md` |
| Soul client feature designs (Camera, CeilingTiles, Appearances) | `SOUL_FEATURES.md` |