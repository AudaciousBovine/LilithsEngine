# Conventions

## Naming Conventions (from README)

| Suffix | Meaning | Example |
|--------|---------|---------|
| `*Patch` | Harmony patch that injects before/after game code | `InitializationPatch`, `ClientConnectPatch` |
| `*Patcher` | Modifies ECS / managed game data | `RecipePatcher`, `LocalizationPatcher`, `DescriptionPatcher`, `IconPatcher` |
| `*Injector` | Injects values into game systems outside ECS | — (LocalizationInjector retired; see LocalizationPatcher) |
| `*Service` | Static class that performs work | `LocalizationService` |
| `*Queue` | Holds work items done at controlled rate | `SyncQueue` |
| `*Builder` | Builds complex objects/data into manageable structures | `CookbookConfigBuilder`, `HeartConfigBuilder` |
| `*Cache` | Stores built data, rebuilt only when values change | `SyncPayloadCache` |
| `*Data` | Runtime container holding data values | `LilithRecipeData`, `CookbookItemData`, `TierBlobData`, `ItemAppearanceData` |
| `*Payload` | Envelope of data for sending over network | `ServerSyncPayload`, `ServerEventPayload` |
| `*Def` | Defines the structure of a single entity | `PrefabDef` |
| `*Index` | Static collection of values for lookup | `WeaponsIndex`, `HeartPathIndex`, `SoulPathIndex`, `HeartEventIndex` |
| `*Enum` | Named set of constant values | `EventKind`, `SyncTierEnum` |
| `*Registry` | Runtime lookup table populated dynamically | `HeartModuleRegistry`, `ServerRegistry` |
| `*Config` | Defines settings and writes config files | `HeartConfig`, `SoulConfig`, `ItemAppearanceConfig` |
| `*Logger` | Logging utility for console messages | `HeartLogger`, `SoulLogger` |
| `*Extensions` | Extension methods for commonly used types | `EntityExtensions` |
| `*Sender` | Sends information over network | `SyncSender` |
| `*Loader` | Reads and merges data for use | `CookbookLoader` |
| `*System` | Recurring logic systems or ECS processing | `RecipeSystem`, `StationSystem` |
| `*Resolver` | Resolves one identifier form to another | `PrefabNameResolver` |
| `*Downloader` | Fetches remote resources | `IconDownloader` |

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
- **For two overloads with different bodies, use two separate `[HarmonyPatch]`
  classes with explicit parameter-type arrays — NOT a shared `TargetMethods()`
  resolver, which mis-applies every postfix in the class to every target.**
- **Avoid patching the client tooltip-build pipeline in this IL2CPP build.**
  `FakeTooltip.SetData`/`SetTooltip` crash when patched; the
  `RefreshGeneralItemTooltip` overloads attach but never fire on inventory
  hovers. Prefer data-layer repointing (see "Appearance overrides" below).

### Registry Pattern
- `HeartModuleRegistry` — modules register themselves by ID for feature discovery
- `ServerRegistry` — maps connection strings to folder names for cache lookup

### Extension Methods
- `EntityExtensions` in both Heart and Soul — fluent ECS operations

### DTO Pattern
- All `*Data.cs` and `*Payload.cs` are plain objects for JSON serialization
- No game dependencies in LilithsMind DTOs

## Appearance Overrides — Data-Layer Repointing (names, descriptions, icons)

All three item-appearance overrides are applied at the **managed data layer**
(`ManagedItemData`), never by patching the UI. Any tooltip/inventory builder
reads `ManagedItemData`, so the game renders our values on its own.

- **Name** — `ManagedItemData.Name` is a value-type `LocalizationKey`. Mint a
  fresh `AssetGuid`, write the string to `Localization._LocalizedStrings`, set
  `Name` to a `LocalizationKey` over the minted guid.
- **Description** — `ManagedItemData.Description` is a value-type struct
  (`LocalizedStringBuilderBase`) whose first field is a `LocalizationKey Key`.
  Same recipe, with a mandatory **struct write-back**: the getter returns a
  copy, so set `.Key` on a local and assign the whole local back to
  `item.Description`. Mutating the getter's copy in place does nothing.
- **Icon** — `ManagedItemData.Icon` is a `Sprite` reference; assign directly.

Each patcher captures originals and restores them in its clear step before the
next apply.

## Performance Practices (documented inline)

- `[PERFORMANCE]` annotations throughout code with O-notation comments
- Debug logging short-circuits: `if (HeartConfig.IsDebug)` check before string concat
- Reflection runs once at startup (GetTypes, GetFields)
- Dictionaries for all lookups (O(1))
- Snapshot dispatch in event bus prevents lock contention
- GetAllEntities noted as ~560K entities — acceptable one-time startup cost
- Payload serialization runs at most twice at startup (baseline + final)
- No per-frame ECS queries after initialization
- Appearance repointing is one-time at apply; steady-state cost is ZERO (no
  getter patch, no per-frame work — the game resolves minted keys natively)

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

Within a payload the order is fixed (9 steps):

1. `LocalizationPatcher.ClearPrevious()` — restore prior repointed display names
2. `LocalizationPatcher.Apply(payload)` — repoint display names (mint key + inject string + point `ManagedItemData.Name`)
3. `DescriptionPatcher.Clear()` — restore prior repointed descriptions
4. `DescriptionPatcher.Build(payload)` — repoint descriptions (mint key + inject string + struct write-back on `ManagedItemData.Description`)
5. `IconPatcher.ClearPrevious()` — restore original icons
6. `IconPatcher.Apply(payload)` — sprites into `ManagedItemData.Icon`
7. `RecipePatcher.Apply(payload.RecipeOverrides)` — recipe ECS data
8. `RecipePatcher.ApplyStationRecipes(payload.StationRecipeOverrides)` — station buffers
9. `RecipePatcher.ApplyPlayerRecipes(...)` — player buffer last

Lookup tables (name→PrefabGUID, sprite maps) are built once in
`NotifyWorldReady()`; the steps above only read them. The fixed ordering
ensures each patcher's clear step runs before its apply, and that names,
descriptions, and icons are in place before the crafting UI reads them.

> **Note the two clear-step method names differ by patcher.**
> `LocalizationPatcher` and `IconPatcher` use `ClearPrevious()`;
> `DescriptionPatcher` uses `Clear()`. This is intentional and documented as-is.

**Names and descriptions are repointed, not overwritten.** Many vanilla items
share one localization key by value; overwriting the string at that key changes
every item sharing it. The retired `LocalizationInjector` also cleared via
`Localization.LoadDefaultLanguage()`, which reloads `_LocalizedStrings` from
disk — wiping its own writes on the second apply (renames reverted to raw
GUIDs). Both `LocalizationPatcher` and `DescriptionPatcher` mint a fresh
`AssetGuid` per item and point the value-type key (`ManagedItemData.Name`, or
the `Key` of the `Description` struct) at it. No shared-key contamination, no
table reload.