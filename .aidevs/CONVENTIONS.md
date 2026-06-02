# Conventions

## Naming Conventions (from README)

| Suffix | Meaning | Example |
|--------|---------|---------|
| `*Patch` | Harmony patch that injects before/after game code | `InitializationPatch`, `ClientConnectPatch` |
| `*Patcher` | Modifies ECS component data | `RecipePatcher` |
| `*Injector` | Injects values into game systems outside ECS | — (LocalizationInjector retired; see LocalizationPatcher) |
| `*Service` | Static class that performs work | `LocalizationService` |
| `*Queue` | Holds work items done at controlled rate | — |
| `*Builder` | Builds complex objects/data into manageable structures | `CookbookConfigBuilder` |
| `*Cache` | Stores built data, rebuilt only when values change | `SyncPayloadCache` |
| `*Data` | Runtime container holding data values | `LilithRecipeData`, `CookbookItemData` |
| `*Payload` | Envelope of data for sending over network | `ServerSyncPayload`, `ServerEventPayload` |
| `*Def` | Defines the structure of a single entity | `PrefabDef` |
| `*Index` | Static collection of values for lookup | `WeaponsIndex`, `HeartPathIndex`, `SoulPathIndex`, `HeartEventIndex` |
| `*Enum` | Named set of constant values | `EventKind` |
| `*Registry` | Runtime lookup table populated dynamically | `HeartModuleRegistry`, `ServerRegistry` |
| `*Config` | Defines settings and writes config files | `HeartConfig`, `SoulConfig`, `ItemAppearanceConfig` |
| `*Logger` | Logging utility for console messages | `HeartLogger`, `SoulLogger` |
| `*Extensions` | Extension methods for commonly used types | `EntityExtensions` |
| `*Sender` | Sends information over network | `SyncSender` |
| `*Loader` | Reads and merges data for use | `CookbookLoader` |
| `*System` | Recurring logic systems or ECS processing | `RecipeSystem`, `StationSystem` |

## Coding Style

- **Namespace:** File-scoped (`namespace X.Y;`) — no braces
- **Access modifiers:** Explicit everywhere
- **Nullable reference types:** Enabled project-wide (`<Nullable>enable</Nullable>`)
- **String comparison:** `StringComparison.Ordinal` preferred over culture-sensitive
- **Variables:** Prefer `var` when type is obvious, explicit otherwise
- **Comment style:**
  - `[CHANGED]` — documents changes from previous iterations
  - `[PERFORMANCE]` — documents performance characteristics and O-notation
- **Project references** use `ProjectReference` in `.csproj`
- **NuGet packages** are declared per-project (not transitively resolved)

## Design Patterns

### Singleton/Static Service
All core service classes are static with an `Initialize()` method:
- `Heart`, `Soul` — static ECS world accessors
- `HeartLogger`, `SoulLogger` — static logging
- `HeartEventBus`, `HeartModuleRegistry` — static infrastructure

### Pub/Sub Event Bus
`HeartEventBus` provides type-safe, thread-safe event dispatch:
- Subscribe: `HeartEventBus.Subscribe<T>(handler)`
- SubscribeOnce: `HeartEventBus.SubscribeOnce<T>(handler)` — auto-unsubscribes after first fire
- Unsubscribe: `HeartEventBus.Unsubscribe<T>(handler)`
- Publish: `HeartEventBus.Publish(new T())` — synchronous, catches per-handler exceptions

### Harmony Patching
- **Postfix** — runs after the original method (used for initialization detection, connect detection)
- **Prefix** — runs before the original method (used for message interception)
- Single-fire guards (`_initialized` bool) prevent re-entry
- All patches named `*Patch.cs`

### Registry Pattern
- `HeartModuleRegistry` — modules register themselves by ID for feature discovery
- `ServerRegistry` — maps connection strings to folder names for cache lookup

### Extension Methods
- `EntityExtensions` in both Heart and Soul — fluent ECS operations

### DTO Pattern
- All `*Data.cs` and `*Payload.cs` are plain objects for JSON serialization
- No game dependencies in LilithsMind DTOs

## Performance Practices (documented inline)

- `[PERFORMANCE]` annotations throughout code with O-notation comments
- Debug logging short-circuits: `if (HeartConfig.IsDebug)` check before string concat
- Reflection runs once at startup (GetTypes, GetFields)
- Dictionaries for all lookups (O(1))
- Snapshot dispatch in event bus prevents lock contention
- No Lazy<T> wrappers — `ConfigEntry<T>.Value` already caches
- GetAllEntities noted as ~560K entities — acceptable one-time startup cost
- Payload serialization runs at most twice at startup (baseline + final)
- No per-frame ECS queries after initialization

## Change Documentation

The codebase has extensive inline change tracking using `[CHANGED]` markers:

```
// [CHANGED] Complete rewrite. Previously read Names/*.json files...
//           PrefabNameExporter has been deleted.
//
// [PERFORMANCE] Reflection runs once at world ready...
```

These are critical for understanding code evolution — always read them.

## EventKind Range Reservation

When adding new events to `ServerEventPayload`, use reserved ranges:

| Range | Module |
|-------|--------|
| 0–99 | Core (reserved) |
| 100–199 | LilithsCookbook |
| 200–299 | LilithsBounty |
| 300–399 | LilithsTreasury |
| 400–499 | LilithsMachinations |

## AI Documentation Stewardship

The `.aidevs/` directory is the single source of truth for agent-facing codebase knowledge. Any structural change to the codebase **must** be reflected here so future AI agents don't rediscover stale information.

When making changes, update the relevant files:

| Change type | Files to update |
|---|---|
| New file, class, or folder added | `CODE_MAP.md` — add entry under correct project/section |
| File moved or renamed | `CODE_MAP.md` — update path + add rename note |
| New project/plugin added | `README.md` — update quick-ref table |
| Architecture or init order changed | `ARCHITECTURE.md` — update sequence diagrams |
| New naming convention established | `CONVENTIONS.md` — add to naming table |
| Data flow or payload format changed | `DATA_FLOW.md` — update pipeline diagrams |
| New domain concept introduced | `GLOSSARY.md` — add term definition |
| New prefab category added | `PREFAB_INDEX.md` — add to definition files table |

This rule exists because the `.aidevs/` docs are the **only** persistent memory AI agents have across sessions. Without updates here, an AI will re-analyze the entire codebase from scratch on every task.

## Module Contract

A LilithsHeart child module must:

1. Reference `LilithsHeart.csproj` via `ProjectReference`
2. Declare `[BepInDependency("audaciousbovine.lilithsheart")]`
3. In `Load()`:
   - Create config via `HeartPathIndex.ModuleConfig("ModuleName")`
   - Call `HeartModuleRegistry.Register(new HeartModuleData { ... })`
   - Subscribe to `Heart.OnInitialized` for ECS-dependent work
4. In `OnHeartInitialized()`:
   - Apply ECS changes
   - Call `Heart.Register*()` methods to queue overrides
5. Fully qualify `MyPluginInfo` as `YourModule.MyPluginInfo` (namespace conflict with Heart)

## Client Payload Application Order (FIXED — DO NOT REORDER)
 
`SyncReceiver` applies each tier independently as its `[[LG:end:T:CKSUM]]`
sentinel arrives (Critical before High before Normal). The disk-cached
pre-apply path runs the same steps in one shot via `ApplyPayload()`.
 
Within a payload the order is fixed:
 
1. `LocalizationPatcher.ClearPrevious()` — restore prior repointed display names
2. `LocalizationPatcher.Apply(payload)` — repoint display names (mint key + inject string + point `ManagedItemData.Name`)
3. `IconPatcher.ClearPrevious()` — restore original icons
4. `IconPatcher.Apply(payload)` — sprites into `ManagedItemData.Icon`
5. `RecipePatcher.Apply(payload.RecipeOverrides)` — recipe ECS data
6. `RecipePatcher.ApplyStationRecipes(payload.StationRecipeOverrides)` — station buffers
7. `RecipePatcher.ApplyPlayerRecipes(...)` — player buffer last
Lookup tables (name→PrefabGUID, sprite maps) are built once in
`NotifyWorldReady()`; the steps above only read them. The fixed ordering
ensures each patcher's `ClearPrevious()` runs before its `Apply()`, and that
display names and icons are in place before the crafting UI reads them.

**Display-name localization changed (LocalizationInjector retired).** The old
injector overwrote the string at an item's existing localization key and called
`Localization.LoadDefaultLanguage()` on clear, which reloaded the entire string
table from disk — wiping any keys it had written when it ran a second time
(pre-apply + server payload), so renames reverted to raw GUIDs on screen.
`LocalizationPatcher` instead **repoints**: it mints a fresh `AssetGuid` per
item (unique by construction), writes the new string there, and points
`ManagedItemData.Name` (a value-type property that persists) at it. No
shared-key contamination, and no `LoadDefaultLanguage` anywhere.

**Tooltips are not yet handled.** `ManagedItemData.Description` is a
reference-type property whose getter returns a copy, so a repoint cannot be
persisted through managed data. Tooltip overrides are currently no-ops pending
a Harmony patch on the tooltip-build path (separate task).