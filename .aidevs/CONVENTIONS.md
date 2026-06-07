# Conventions

## Naming Conventions

| Suffix | Meaning | Example |
|--------|---------|---------|
| `*Patch` | Harmony patch that injects before/after game code | `InitializationPatch`, `ClientConnectPatch`, `ServerChatSystemPatch` |
| `*Patcher` | Modifies ECS / managed game data | `RecipePatcher`, `LocalizationPatcher`, `DescriptionPatcher`, `IconPatcher` |
| `*Injector` | Injects values into game systems outside ECS | — (LocalizationInjector retired; see LocalizationPatcher) |
| `*Service` | Static class that performs work | `LocalizationService`, `ItemService`, `LocalizationFileService` |
| `*FileService` | Static service that specifically owns file I/O for a domain | `LocalizationFileService` |
| `*Queue` | Holds work items done at controlled rate | `SyncQueue` |
| `*Builder` | Builds complex objects/data into manageable structures | `CookbookConfigBuilder`, `HeartConfigBuilder` |
| `*Cache` | Stores built data, rebuilt only when values change | `SyncPayloadCache` |
| `*Data` | Runtime container holding data values | `LilithRecipeData`, `CookbookItemData`, `TierBlobData`, `LilithItemData` |
| `*Payload` | Envelope of data for sending over network | `ServerSyncPayload`, `ServerEventPayload` |
| `*Def` | Defines the structure of a single entity | `PrefabDef` |
| `*Index` | Static collection of values for lookup | `WeaponsIndex`, `HeartPathIndex`, `SoulPathIndex`, `HeartEventIndex` |
| `*Enum` | Named set of constant values | `SyncTierEnum`, `SyncModeEnum`, `LanguageCodeEnum` |
| `*Registry` | Runtime lookup table populated dynamically | `HeartModuleRegistry`, `ServerRegistry` |
| `*Config` | Defines settings and writes config files | `HeartConfig`, `SoulConfig`, `LilithItemConfig` |
| `*Logger` | Logging utility for console messages | `HeartLogger`, `SoulLogger` |
| `*Extensions` | Extension methods for commonly used types | `EntityExtensions` |
| `*Sender` | Sends information over network | `SyncSender`, `LocalizationSyncSender` |
| `*Fetcher` | Fetches remote data asynchronously | `SyncHttpFetcher` |
| `*Loader` | Reads and merges data for use | `CookbookLoader` |
| `*System` | Recurring logic systems or ECS processing | `RecipeSystem`, `StationSystem`, `PrisonerFeedSystem` |
| `*Resolver` | Resolves one identifier form to another | `PrefabNameResolver` |
| `*Downloader` | Fetches remote resources | `IconDownloader` |
| `*Feature` | Soul-internal client feature area entry point | `CameraFeature`, `CeilingTileFeature`, `AppearanceFeature` |
| `*Applicator` | Applies data to live game objects (Soul-side) | `AppearanceApplicator` |
| `*Receiver` | Receives and interprets incoming network data (Soul-side) | `AppearanceSyncReceiver` |
| `*Store` | Owns persistent read/write of a managed data domain | `AppearanceStore` |

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

## Enum Location Convention

All shared enums live in `LilithsMind/Data/` — not in `LilithsMind/Network/`. Network/ is for wire DTOs only. Examples: `SyncTierEnum`, `SyncModeEnum`, `LanguageCodeEnum`.

## Config Key Convention

All config files use no spaces in key names (e.g. `ChangesEnabled`, `StackSize`). BepInEx `.cfg` keys also use no spaces.

## Module Config File Convention

All module `.cfg` files live under `BepInEx/config/LilithsHeart/` using `HeartPathIndex.ModuleConfig("ModuleName")`. Not in the module's own config directory. This keeps all LilithsEngine configuration under one root.

## JSON Deserialization Convention

Module config loaders deserialize with `PropertyNameCaseInsensitive = true`. **Never register `JsonStringEnumConverter` globally** in a loader's `JsonSerializerOptions` — on .NET 6 a global string-enum converter can silently null out nullable value-type fields (`float?`, `bool?`, `int?`) in surrounding objects during deserialization of a complex graph. Instead, scope it per-field with `[JsonConverter(typeof(JsonStringEnumConverter))]` on the specific enum property (e.g. `PrisonerFeedEntryData.Type`). See `CookbookLoader` / `CookbookPrisonerFeedData`.

## Design Patterns

### Singleton/Static Service
All core service classes are static with an `Initialize()` method:
- `Heart`, `Soul` — static ECS world accessors
- `HeartLogger`, `SoulLogger` — static logging
- `HeartEventBus`, `HeartModuleRegistry` — static infrastructure
- `SoulEventBus`, `SoulOptionsRegistry` — static Soul-side infrastructure

### Pub/Sub Event Bus
`HeartEventBus` (Heart-side) and `SoulEventBus` (Soul-side) both provide type-safe, thread-safe event dispatch. They are separate buses — `SoulEventBus` is entirely local to the Soul process and is never used for cross-plugin communication. Both follow the same API convention:
- Subscribe: `HeartEventBus.Subscribe<T>(handler)`
- SubscribeOnce: `HeartEventBus.SubscribeOnce<T>(handler)` — auto-unsubscribes after first fire
- Unsubscribe: `HeartEventBus.Unsubscribe<T>(handler)`
- Publish: `HeartEventBus.Publish(new T())` — synchronous, catches per-handler exceptions

### Harmony Patching
- **Postfix** — runs after the original method (used for initialization detection, connect detection)
- **Prefix** — runs before the original method (used for message interception)
- Single-fire guards (`_initialized` bool) prevent re-entry
- All patches named `*Patch.cs`
- **For two overloads with different bodies, use two separate `[HarmonyPatch]` classes with explicit parameter-type arrays.**
- **Avoid patching the client tooltip-build pipeline in this IL2CPP build.** `FakeTooltip.SetData`/`SetTooltip` crash when patched; `RefreshGeneralItemTooltip` overloads attach but never fire on inventory hovers. Prefer data-layer repointing.

### Registry Pattern
- `HeartModuleRegistry` — modules register themselves by ID for feature discovery
- `ServerRegistry` — maps connection strings to folder names for cache lookup

### Extension Methods
- `EntityExtensions` in both Heart and Soul — fluent ECS operations

### DTO Pattern
- All `*Data.cs` and `*Payload.cs` are plain objects for JSON serialization
- No game dependencies in LilithsMind DTOs

### Module Enable/Disable Pattern
Modules check `ModuleEnabled` immediately after config init in `Load()`:
```csharp
CookbookConfig.Initialize(configFile);
if (!CookbookConfig.ModuleEnabled)
{
    HeartLogger.Info(LOG_SOURCE, "Disabled via ModuleEnabled=false. Skipping.");
    return;
}
```
When disabled: no ECS work, no generator registration, no Heart subscription.

### Soul Client Feature Enable/Disable Pattern

Soul client feature areas (Camera, CeilingTiles, Appearances) follow the same
zero-cost-when-disabled principle as Heart modules, enforced at `SoulPlugin.Load()`:

```csharp
if (SoulConfig.CameraEnabled)
    CameraFeature.Initialize();

if (SoulConfig.CeilingTilesDefaultEnabled)
    CeilingTileFeature.Initialize();

if (SoulConfig.AppearancesEnabled)
    AppearanceFeature.Initialize();
```

When a feature flag is `false`: no Harmony patches registered for that feature,
no hooks, no GameObjects, no memory held, no per-frame cost. The config flag is
read once at load time — changes require a restart.

This mirrors the Heart module `ModuleEnabled` early-exit pattern. All Soul client
feature flags default to `false` (opt-in philosophy).

### Heart Does Minimum — Soul Does Work

**Principle:** Heart's role is to be a reliable, low-overhead authority. Soul is
responsible for all presentation-layer and optional work on the player's machine.

When deciding which side should perform a task, apply this test: if the work is
optional, involves local resources (disk, HTTP, Unity GameObjects, textures), or
exists purely to serve one player's experience — it belongs in Soul.

Examples of this principle applied:
- URL texture fetching and caching — Soul only; Heart never performs HTTP requests
- Appearance rendering and texture application — Soul only
- Client-side whitelist filtering — Soul only; Heart broadcasts to all, Soul filters locally
- Cooldown tracking after Heart's initial response — Soul suppresses resends locally

Heart's appearance obligations are limited to: permission check, cooldown check,
write to disk, broadcast payload. Nothing more.

### Config Generation Pattern
Modules register their generators with `HeartConfigBuilder` before Heart initializes:
```csharp
HeartConfigBuilder.RegisterExampleGenerator(CookbookConfigBuilder.GenerateExampleFiles);
HeartConfigBuilder.RegisterDebugGenerator(CookbookConfigBuilder.GenerateDebugFiles);
```
All example and debug files are stored as embedded JSON resources in `Resources/Examples/` and `Resources/Debug/` subfolders, extracted on demand. Always overwrite — no skip-if-exists.

### Embedded Resource Convention
JSON config templates are embedded resources in each module's DLL:
- `LilithsHeart/Resources/Examples/Examples_*.json`
- `LilithsHeart/Resources/Debug/Debug_*.json`
- `LilithsCookbook/Resources/Examples/Examples_*.json`
- `LilithsCookbook/Resources/Debug/Debug_*.json`
Resource name format: `<AssemblyName>.Resources.Examples.<FileName>` or `<AssemblyName>.Resources.Debug.<FileName>`.

### Combined Config File Convention
A single Recipes/*.json file may carry multiple typed blocks under separate top-level keys via a `file`-scoped wrapper type (e.g. `CookbookRecipeFile` with `Recipes` and `PrisonerFeeding`). Loaders deserialize the wrapper once and split into the respective containers. Recipe and prisoner-feed entries are keyed by prefab name or LilithsMind Name alias.

## Appearance Overrides — Data-Layer Repointing

All three item-appearance overrides are applied at the **managed data layer** (`ManagedItemData`), never by patching the UI.

- **Name** — `ManagedItemData.Name` is a value-type `LocalizationKey`. Mint a fresh `AssetGuid`, write the string to `Localization._LocalizedStrings`, set `Name` to a `LocalizationKey` over the minted guid.
- **Description** — `ManagedItemData.Description` is a value-type struct (`LocalizedStringBuilderBase`) whose first field is `LocalizationKey Key`. Same recipe, with a mandatory **struct write-back**: the getter returns a copy, so set `.Key` on a local and assign the whole local back to `item.Description`.
- **Icon** — `ManagedItemData.Icon` is a `Sprite` reference; assign directly.

Each patcher captures originals and restores them in its clear step before the next apply.

### Color Tag Translation
Injected strings (names, descriptions) bypass V Rising's named-colour-tag processing layer, so V Rising tags (`<teal1>`, `</c>`, etc.) render literally. `ColorTranslator.Translate()` (LilithsSoul) converts V Rising tags to Unity rich text (`<color=#...>`, `</color>`) before the string is written to `_LocalizedStrings`. Both patchers call it at inject time. Unity rich text is processed at the render layer and works directly. Admins may use either tag style interchangeably in config.

## ECS Write Ordering — GameDatas Lookup Maps Are Authoritative, Write Them Last

**V Rising's `GameDatas` struct holds several `NativeParallelHashMap<PrefabGUID, T>` lookup maps that are the AUTHORITATIVE source for their data — not the prefab entity components.** Writing only the prefab entity component leaves the map holding vanilla values, and the game reads the map. Confirmed maps and the systems that read them:

| Map | Value type | Read by | Field controlling |
|-----|-----------|---------|-------------------|
| `RecipeHashLookupMap` | `RecipeData` | Crafting completion system | `CraftDuration`, `AlwaysUnlocked`, `HideInStation`, etc. |
| `ItemHashLookupMap` | `ItemData` | Inventory system | `MaxAmount` (stack size) |

(Other maps exist on `GameDatas` — `ItemGroupHashLookupMap`, `DropTableDataHashLookupMap`, `BlueprintHashLookupMap`, `StationBonusLookupMap` — and almost certainly follow the same pattern. Treat any future `*HashLookupMap` as authoritative until proven otherwise.)

The trap is a **entity-vs-map split**: the entity component write often drives a *display* path (e.g. the crafting countdown timer), so the value LOOKS applied, while the map still holds vanilla and the game's actual logic uses it. Symptoms seen: recipes counting down correctly then failing at completion and reverting to 86400s (24h); stack sizes appearing set but items stacking past the limit.

`RegisterRecipes()` and `RegisterGameData()` **rebuild these maps from baked scene data**, wiping any map writes made before them.

Rules:
- **Entity component writes** (`recipeEntity.Write(data)`, `entity.Write(itemData)`) survive registration — do these in the normal apply pass. Still write them; some display paths read the entity.
- **Map writes** (`map[guid] = entry`) must be the **final ECS mutation** in the whole module init sequence, after *every* `RegisterRecipes()`/`RegisterGameData()` call across *all* systems. Access via `Heart.GameDataSystem.<MapName>`.
- In Cookbook this is enforced by ordering in `CookbookPlugin.OnHeartInitialized()`:
  - `RecipeSystem.ApplyChanges()` — entity + buffers + own `RegisterRecipes()`
  - `StationSystem.ApplyChanges()` — calls both registration methods
  - `ItemFunctionService.ApplyOverrides()` — writes both the item prefab entity AND `ItemHashLookupMap` (runs after StationSystem's `RegisterGameData()`, so its map write is safe)
  - `RecipeSystem.ApplyMapValues()` called **LAST** — `RecipeHashLookupMap` scalar writes only
- Any future module that modifies a field backed by a `GameDatas` lookup map AND calls a register method (e.g. Grimoire spell stats, Armory weapon stats) must follow the same write-the-map-last ordering.
- Build Soul override DTOs from the **config entry** value, not by reading back the entity/map, so the synced value is correct regardless of ECS state during the multi-system init sequence.

## Performance Practices

- `[PERFORMANCE]` annotations throughout code with O-notation comments
- Debug logging short-circuits: `if (HeartConfig.IsDebug)` check before string concat
- Reflection runs once at startup (GetTypes, GetFields)
- Dictionaries for all lookups (O(1))
- Snapshot dispatch in event bus prevents lock contention
- Payload serialization runs at most twice at startup (baseline + final)
- No per-frame ECS queries after initialization
- Appearance repointing is one-time at apply — zero steady-state cost
- Soul client features (Camera, CeilingTiles, Appearances) incur zero cost when
  their feature flag is `false` — no patches registered, no hooks, no per-frame work
- Ceiling tile grid rebuilds are boundary-triggered only — never per frame
- Appearance URL textures are lazy-loaded and disk-cached by URL hash — second
  encounter is always instant; Heart never fetches textures

## Change Documentation

The codebase has extensive inline change tracking using `[CHANGED]` markers. Always read them — they explain why code is the way it is.

## Client Payload Application Order (FIXED — DO NOT REORDER)

See `ARCHITECTURE.md` — Payload Application Order section. The 9-step order is fixed and must not change.

> **Note the two clear-step method names differ by patcher.**
> `LocalizationPatcher` and `IconPatcher` use `ClearPrevious()`;
> `DescriptionPatcher` uses `Clear()`. Intentional and documented as-is.

## Soul→Heart Communication

All Soul→Heart communication uses the `[[LG:...]]` sentinel pattern via `ChatMessageEvent { MessageType = Local }` in the client ECS world. Heart intercepts via `ServerChatSystemPatch`. VCF is a server-side framework — Soul has no VCF dependency and must never gain one.

Current Soul→Heart sentinels:
- `[[LG:sync-fallback]]` — HTTP fetch failed, request chunk delivery
- `[[LG:lang-request:<language>]]` — request localization payload for a language
- `[[LG:appearance:update:<payload>]]` — player submitting their active appearance preset
- `[[LG:appearance:clear]]` — player clearing their own appearance

Current Heart→Soul sentinels (also handled in ServerChatSystemPatch):
- `[[LG:sync-url:<url>:<fallback>]]` — redirect client to fetch payload from URL
- `[[LG:lang-unavailable:<language>]]` — requested language not configured on server
- `[[LG:appearance:data:<steamid>:<payload>]]` — full appearance snapshot for a player
- `[[LG:appearance:clear:<steamid>]]` — remove a player's appearance from all clients
- `[[LG:appearance:cooldown:<seconds>]]` — cooldown remaining, sent to requesting client only
- `[[LG:appearance:maxweapons:<n>]]` — server's MaxWeaponAppearances setting, sent on connect

All new Soul→Heart sentinels must be added to `ServerChatSystemPatch.cs` — the single home for this communication.

## AI Documentation Stewardship

The `.aidevs/` directory is the single source of truth for agent-facing codebase knowledge. Any structural change to the codebase **must** be reflected here.

| Change type | Files to update |
|---|---|
| New file, class, or folder added | `CODE_MAP.md` — add entry under correct project/section |
| File moved or renamed | `CODE_MAP.md` — update path |
| New project/plugin added | `README.md` — update module table |
| Architecture or init order changed | `ARCHITECTURE.md` — update sequence diagrams |
| New naming convention established | `CONVENTIONS.md` — add to naming table |
| Data flow or payload format changed | `DATA_FLOW.md` — update pipeline diagrams |
| New domain concept introduced | `GLOSSARY.md` — add term definition |
| New prefab category added | `PREFAB_INDEX.md` — update |

## Module Contract

A LilithsHeart child module must:
1. Reference `LilithsHeart.csproj` via `ProjectReference`
2. Declare `[BepInDependency("audaciousbovine.lilithsheart")]`
3. In `Load()`:
   - Check `ModuleEnabled` immediately after config init — return early if false
   - Register example/debug generators with `HeartConfigBuilder`
   - Call `HeartModuleRegistry.Register(new HeartModuleData { ... })`
   - Subscribe to `Heart.OnInitialized`
4. In `OnHeartInitialized()`: apply ECS changes, call `Heart.Register*()` methods
5. Fully qualify `MyPluginInfo` as `YourModule.MyPluginInfo` (namespace conflict with Heart)
6. Communicate with other modules exclusively via `HeartEventBus` — no direct cross-module references

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

> These ranges are for **Heart-side** (`HeartEventBus`) events only.
> Soul-side (`SoulEventBus`) event ranges are documented in `ARCHITECTURE.md`
> under the SoulEventBus section.