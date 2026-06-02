# Glossary

## Project Terminology

| Term | Definition |
|------|------------|
| **LilithsGarden** | The overall mod suite name. Thematic naming: Heart (server core), Soul (client core), Mind (shared knowledge), Cookbook (recipe module). |
| **Heart** | `LilithsHeart` — server-side plugin that manages ECS access, module registration, and sync payload delivery. |
| **Soul** | `LilithsSoul` — client-side plugin that intercepts chat messages, patches local ECS entities, injects localization, and renders custom UI panels. |
| **Mind** | `LilithsMind` — shared C# library with zero game dependencies. Holds prefab definitions and network DTOs. |
| **Cookbook** | `LilithsCookbook` — server-side child module of Heart that reads JSON config files and applies recipe/station changes. |
| **Child Module** | A BepInEx plugin that depends on Heart (`[BepInDependency("audaciousbovine.lilithsheart")]`) and registers via `HeartModuleRegistry`. |
| **Stash** | A server-side logical item store per player, entirely outside ECS. Exists in two forms: PlayerStash (carried, drops on PvP death) and CastleStash (bound to castle, never drops). Managed by LilithsTreasury. |
| **StashItem** | A named key + quantity entry in a player's stash store. Not a real ECS entity — purely managed C# data persisted to JSON. |
| **BackingItem** | The vanilla PrefabGUID item that a stash item semantically represents. Used for convert/redeem operations between real ECS inventory and the stash. |
| **Semantic Item Variant** | A stash item that represents a meaningful subset of a vanilla item (e.g. "Oak" and "Birch" as variants of `Item_Resource_Wood`). The vanilla inventory sees only Wood; the stash tracks the variant. |
| **Materialisation** | The act of converting a stash item into a real ECS item entity in the player's vanilla inventory, or the reverse. Used when stash items need to interact with vanilla game systems. |
| **Custom Crafting** | A server-defined crafting system entirely outside vanilla ECS. Recipes are JSON config; ingredients and outputs can be stash items, vanilla items, or both. Operated via the Soul custom crafting panel. |
| **Proximity Trigger** | A Soul-side system that monitors player distance to known furniture entities and opens a custom UI panel when the player is within range and presses the interact key. Used as the interaction model for all custom station panels. |
| **Silent Command** | A VCF command fired programmatically by Soul on the player's behalf without displaying it in chat. Used to send player interaction events (stash take/deposit, craft, teleport) back to Heart. |
| **Ritual** | A LilithsBlessings construct — a structured sacrifice recipe requiring items, vanilla resources, or dominated unit sacrifices that, when fully completed, applies a buff to a defined scope (player, clan, or server). |
| **Expedition** | A time-based mission assigned to custom units (Conquest) or creatures (Menagerie). Runs as a server-side timer; outcome is simulated by Heart on completion. |
| **MenagerieCreature** | A persistent managed C# data record representing a captured/bred creature in LilithsMenagerie. Not a real ECS entity — stats, traits, lineage, and current activity stored in JSON. |
| **Infamy** | A per-player, per-faction reputation value tracked by LilithsAdversaries. Increases when the player provokes a faction; decays over real time. Triggers escalating faction responses at configured thresholds. |
| **Schematic** | A LilithsArchitects JSON definition describing a complete castle layout as a set of tile/object placements relative to an origin point. Placed via Kindred Schematics integration. |
| **HeartEventBus** | The pub/sub event system in LilithsHeart. All cross-module events (kills, crafts, captures, ritual completions, etc.) are published here. LilithsMachinations is the primary consumer for quest objective tracking. |

## V Rising / ECS Terminology

| Term | Definition |
|------|------------|
| **ECS** | Entity Component System — Unity's DOTS architecture. V Rising uses this internally. Entities are IDs, Components are data structs, Systems process them. |
| **Entity** | An `Entity` struct — a lightweight ID referencing a collection of components in the ECS world. |
| **Prefab** | A template Entity stored in `PrefabCollectionSystem._PrefabGuidToEntityMap`. Recipes, items, stations, etc. all have prefab entities that define their base state. |
| **PrefabGUID** | `Stunlock.Core.PrefabGUID` — wraps a single `int` (`_Value`) identifying a prefab. The identity key for all item types, recipes, spells, units, and building tiles in V Rising. |
| **AssetGuid** | `Stunlock.Core.AssetGuid` — a GUID type used as the key in `Localization._LocalizedStrings`. Maps to NameKey/DescKey strings in PrefabDef. |
| **Component** | A struct implementing `IComponentData` attached to entities. Examples: `RecipeData`, `NetworkId`, `User`. |
| **DynamicBuffer** | `Unity.Entities.DynamicBuffer<T>` — a resizable array buffer component. Examples: `RecipeRequirementBuffer`, `WorkstationRecipesBuffer`. |
| **World** | `Unity.Entities.World` — a container for entities and systems. V Rising has separate server and client worlds. |
| **EntityManager** | `Unity.Entities.EntityManager` — the API for creating, reading, writing, and destroying entities and components. |
| **System** | A class that processes entities. Accessible via `World.GetExistingSystemManaged<T>()`. |
| **GameDataSystem** | V Rising's system holding `RecipeHashLookupMap` — a dictionary mapping `PrefabGUID → RecipeData` that the crafting UI reads. |
| **PrefabCollectionSystem** | V Rising's system holding `_PrefabGuidToEntityMap` (PrefabGUID → Entity) and `_PrefabDataLookup` (PrefabGUID → PrefabData). |
| **RecipeHashLookupMap** | A `NativeHashMap<PrefabGUID, RecipeData>` in `GameDataSystem`. Crafting reads scalar fields from this map, not from entity components. |
| **WorkstationRecipesBuffer** | `DynamicBuffer<WorkstationRecipesBuffer>` — defines which recipes appear at a crafting station or for a player. |
| **RefinementstationRecipesBuffer** | `DynamicBuffer<RefinementstationRecipesBuffer>` — defines recipes for automatic refinement stations (Furnace, Grinder). |
| **Archetype** | The exact set of component types on an entity. Unity DOTS stores entities in memory chunks organised by archetype. Adding or removing components moves an entity to a different chunk. V Rising's systems query for fixed known archetypes — mutating an entity's archetype unexpectedly is unsafe. |
| **IL2CPP** | Ahead-of-time compilation mode used by V Rising. Game code is compiled to native C++ and cannot be directly modified. BepInEx + HarmonyLib patch it via runtime method replacement. |

## Sync Protocol Terminology

| Term | Definition |
|------|------------|
| **ServerSyncPayload** | The main data contract sent from Heart to Soul on client connect. Contains localization overrides, recipe overrides, station changes, and player recipe changes. |
| **ServerEventPayload** | A future payload type for in-session events (not yet implemented). Uses `EventKind` for routing. |
| **StashPayload** | A per-player targeted payload containing current stash contents. Sent on connect and on any stash change. Separate from the connect-time ServerSyncPayload. |
| **PayloadHash** | First 8 hex characters of SHA256 hash of the serialized payload. Used by Soul to skip redundant disk writes and re-injection on reconnect. |
| **Chunk** | A 450-character fragment of the JSON payload, sent as a `ChatMessageServerEvent` with `[[LG:N]]` prefix. |
| **[[LG:end]]** | Sentinel message telling Soul the payload is complete and ready to reassemble. |
| **ChatMessageServerEvent** | V Rising's network event type for system chat messages. Used as transport because Unity Netcode is unavailable in IL2CPP. |
| **ServerIdentity** | The sanitized server name from `HeartConfig.ServerName`. Used as a folder name on the client for per-server cached data. |
| **SyncHttpServer** | A planned minimal `HttpListener` in Heart that serves the current sync payload as a static JSON endpoint on a configured side port. Allows Soul to fetch the payload via HTTP rather than the chunk transport. |
| **SyncHttpFetcher** | A planned Soul-side `UnityWebRequest` fetcher that attempts to retrieve the sync payload from the Heart HTTP endpoint at world ready, before falling back to the chunk transport. |

## Config / Data Terminology

| Term | Definition |
|------|------------|
| **PrefabDef** | A `readonly record struct` in LilithsMind defining a single prefab's metadata (Name, GuidHash, Prefab, NameKey, DescKey). |
| **NameKey** | A GUID string (e.g. `"37e872e1-4aa1-4f0a-8e2e-a67883b5a645"`) that maps to a display name in `Localization._LocalizedStrings`. |
| **DescKey** | A GUID string that maps to a tooltip/description in `Localization._LocalizedStrings`. |
| **CookbookRecipeData** | Deserialized from `Recipes/*.json`. Contains `Dictionary<string, RecipeEntryData>`. Uses `CookbookItemData` for requirements, outputs, repair costs, and unit outputs. |
| **RecipeEntryData** | A config DTO with scalar fields (`CraftDuration`, `AlwaysUnlocked`, etc.) and optional buffer lists (`Requirements`, `Outputs`, `RepairCosts`, `UnitOutputs`, `RecipeLinks`). Renamed from `RecipeEntry`. |
| **CookbookStationData** | Deserialized from `Stations/*.json`. Contains `Dictionary<string, StationEntryData>`. |
| **StationEntryData** | A config DTO with `ChangesEnabled`, `AddRecipes`, and `RemoveRecipes`. Renamed from `StationEntry`. |
| **CookbookItemData** | A single item+amount DTO (`Item` string, `Amount` int) used across all recipe slot contexts (requirements, outputs, repair costs, unit outputs). Consolidates the previous `RecipeRequirement`, `RecipeOutput`, `RecipeRepairCost`, and `RecipeUnitOutput` classes. |
| **LilithRecipeData** | Network DTO in `ServerSyncPayload.RecipeOverrides` — contains `CraftDuration`, `Requirements` (Dictionary<string,int>), and `Outputs` (Dictionary<string,int>). |
| **LilithStationData** | Network DTO in `ServerSyncPayload.StationRecipeOverrides` — contains `RecipesToAdd` and `RecipesToRemove`. |
| **StashItemDef** | Config DTO defining a custom stash item — Key, DisplayName, Icon, Tooltip, optional BackingItem (vanilla prefab name), optional ConvertRatio. |
| **MenagerieCreatureDef** | Config DTO defining a creature species/breed — species, stat ranges, trait pool, production type and rates, max generation stat ceilings. |
| **RitualDef** | Config DTO defining a Blessings ritual — required sacrifices (items, vanilla resources, dominated units), target scope (player/clan/server), buff applied, duration. |
| **QuestDef** | Config DTO defining a Machinations quest — key, type (daily/weekly/repeatable/chain), objectives list, rewards list. |
| **ExpeditionDef** | Config DTO defining a Conquest or Menagerie expedition — key, duration, difficulty, required unit types, reward table. |

## Development / Code Organization

| Term | Definition |
|------|------------|
| **`[CHANGED]`** | Inline comment marker documenting changes from previous code iterations. Essential for understanding evolution. |
| **`[PERFORMANCE]`** | Inline comment marker documenting performance characteristics and O-notation of operations. |
| **Harmony Patch** | A `[HarmonyPrefix]` or `[HarmonyPostfix]` method that injects code before or after a game method. Named `*Patch.cs`. |
| **Single-fire Guard** | A `static bool _initialized` field that prevents a Harmony patch from executing more than once (e.g., world init detection). |
| **`using static`** | Not used in this codebase. All usages are explicit. |
| **Write-ahead Pattern** | For stash operations: credit the stash store before consuming the real ECS item. Ensures no item loss in the event of a server crash mid-transaction. |
| **Two-pass Patching** | Pattern established in StationSystem — Pass 1 patches prefab entities, Pass 2 patches live world entities after RegisterGameData(). Required because RegisterGameData() resets live entity buffers but not prefab entities. |

## EventKind Range Reservation

| Range | Module |
|-------|--------|
| 0–99 | Core (reserved) |
| 100–199 | LilithsCookbook |
| 200–299 | LilithsBounty |
| 300–399 | LilithsTreasury |
| 400–499 | LilithsMachinations |
| 500–599 | LilithsAdversaries |
| 600–699 | LilithsConquest |
| 700–799 | LilithsMenagerie |
| 800–899 | LilithsBlessings |
| 900–999 | LilithsWisdom |
| 1000–1099 | LilithsNexus |
| 1100–1199 | LilithsGrimoire |
| 1200–1299 | LilithsArchitects |
| 1300–1399 | LilithsMachinations (extended) |