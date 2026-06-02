# Code Map — File-by-File Index

## Root Files

| File | Purpose |
|------|---------|
| `LilithsGarden.sln` | Visual Studio solution referencing 4 projects |
| `Directory.Build.props` | Shared MSBuild properties (net6.0, C# 12, nullable, VRising.Unhollowed.Client) |
| `global.json` | Pins .NET SDK to 8.0.421 |
| `README.md` | Project description + naming conventions |

---

## LilithsMind (Shared Library — Pure C#)

### Root

| File | Purpose |
|------|---------|
| `LilithsMind.csproj` | Project file, no NuGet refs, version 0.1.0 |

### Data/

| File | Class | Purpose |
|------|-------|---------|
| `ItemAppearanceData.cs` | `ItemAppearanceData` | DTO with optional `DisplayName`, `Tooltip`, `Icon` fields. Value type in `ServerSyncPayload.ItemAppearanceOverrides`. Icon value is self-describing: filename → local PNG, sprite name → in-game sprite, https:// → URL download. |

### Prefabs/

| File | Class | Purpose |
|------|-------|---------|
| `PrefabDef.cs` | `PrefabDef` | `readonly record struct` — universal prefab definition (Name, GuidHash, Prefab, NameKey, DescKey). Stack-allocated, zero heap pressure. **`NameKey` is no longer required for display-name overrides** (LocalizationPatcher mints fresh keys and reads the live name by PrefabGUID); `NameKey`/`DescKey` are retained as a vanilla-key reference record and for the pending tooltip work. |

### Prefabs/Definitions/ — 22 static index classes

| File | Contains |
|------|----------|
| `WeaponsIndex.cs` | All weapon items (swords, axes, maces, spears, pistols, etc.) — ~1453 lines |
| `StationsIndex.cs` | All crafting/refinement station prefabs |
| `ArmorHeadIndex.cs` | Helmet/head armor prefabs |
| `ArmorChestIndex.cs` | Chest armor prefabs |
| `ArmorLegsIndex.cs` | Leg armor prefabs |
| `ArmorGlovesIndex.cs` | Glove armor prefabs |
| `ArmorBootsIndex.cs` | Boot armor prefabs |
| `ArmorCloakIndex.cs` | Cloak prefabs |
| `AccessoryIndex.cs` | Rings, sources, necklaces — fully filled out with NameKey/DescKey |
| `BagIndex.cs` | Bag/container prefabs |
| `SaddleIndex.cs` | Mount saddle prefabs |
| `ItemsResourcesIndex.cs` | Resource items (minerals, ingots, lumber, etc.) |
| `ItemsUsableIndex.cs` | Usable/consumable items |
| `ItemsMiscIndex.cs` | Miscellaneous items |
| `ItemsJewelIndex.cs` | Jewel/gem items |
| `ItemsBookIndex.cs` | Book/schematics items |
| `RecipesWeaponIndex.cs` | Weapon recipe prefabs |
| `RecipesUseableIndex.cs` | Usable item recipe prefabs |
| `RecipesResourceIndex.cs` | Resource recipe prefabs |
| `RecipesMiscIndex.cs` | Miscellaneous recipe prefabs |
| `RecipesJewelIndex.cs` | Jewel recipe prefabs |
| `RecipesEquipmentIndex.cs` | Equipment recipe prefabs |

### Network/

| File | Class | Purpose |
|------|-------|---------|
| `ServerSyncPayload.cs` | `ServerSyncPayload` | Full data contract: identity, hash, `ItemAppearanceOverrides: Dictionary<string, ItemAppearanceData>` (replaces separate DisplayName/Tooltip dicts), recipe overrides, station overrides, player recipe changes. |
| `ServerEventPayload.cs` | `ServerEventPayload`, `EventKind` | Trigger-based in-session payload. Reserved — not yet implemented. |

---

## LilithsHeart (Server Plugin)

### Root

| File | Class | Purpose |
|------|-------|---------|
| `HeartPlugin.cs` | `HeartPlugin : BasePlugin` | BepInEx entry point. Initializes logger, config, event bus, module registry, Harmony patches. |
| `LilithsHeart.csproj` | — | Net6.0, references Mind, VRising.Unhollowed.Client, VCF |

### Foundation/

| File | Class | Purpose |
|------|-------|---------|
| `Heart.cs` | `Heart` | Server world access, ECS system accessors, module registration API. Fires `OnInitialized` and `OnWorldReady`. |
| `HeartLogger.cs` | `HeartLogger` | Server logging wrapper. |
| `EntityExtensions.cs` | `EntityExtensions` | Fluent ECS extension methods using `Heart.EntityManager`. |

### Events/

| File | Class | Purpose |
|------|-------|---------|
| `HeartEventBus.cs` | `HeartEventBus` | Type-safe pub/sub event bus. Thread-safe via lock. Snapshot dispatch. |
| `HeartEventIndex.cs` | `OnWorldReady` | Event types published by Heart. |

### Modules/

| File | Class | Purpose |
|------|-------|---------|
| `HeartModuleRegistry.cs` | `HeartModuleRegistry` | Runtime registry of loaded child modules. `Register()`, `LogSummary()`. |
| `HeartModuleData.cs` | `HeartModuleData` | Module identity: `ModuleId`, `ModuleName`, `Version`. |

### Patches/

| File | Class | Purpose |
|------|-------|---------|
| `InitializationPatch.cs` | `InitializationPatch` | Harmony postfix on `WarEventRegistrySystem.RegisterWarEventEntities`. Single-fire — calls `Heart.OnInitialize()`. |
| `ClientConnectPatch.cs` | `ClientConnectPatch` | Harmony postfix on `ServerBootstrapSystem.OnUserConnected`. Resolves User + Character entities + userIndex, calls `SyncSender.EnqueueSyncTiers()`. |
| `SchedulerPatch.cs` | `SchedulerPatch` | Harmony postfix on `ServerBootstrapSystem.OnUpdate`. Per-frame drain of `SyncQueue` at `ChunksPerFrame` rate. Fast-path: single `HasPending` bool check when idle. |

### Network/

| File | Class | Purpose |
|------|-------|---------|
| `SyncTierEnum.cs` | `SyncTierEnum` | Priority tiers: `Critical(0)`, `High(1)`, `Normal(2)`, `Low(3)`, `Background(4)`. Lower = higher priority = sent first. |
| `TierBlobData.cs` | `TierBlobData` | Pre-built chunk data for one tier: `Tier`, `Chunks[]` (base64+gzip strings), `ChunkCount`, `Checksum`. Immutable after construction. |
| `SyncQueue.cs` | `SyncQueue` | Thread-safe FIFO queue of pending client sends. `Enqueue()` on connect, `Drain()` each frame. `ChunksPerFrame = 10`. |
| `SyncSender.cs` | `SyncSender` | `EnqueueSyncTiers()` builds tier messages from `TierBlobData`, enqueues into `SyncQueue`. `SendQueuedChunk()` creates one `ChatMessageServerEvent` entity with `SendEventToUser`. Protocol: `[[LG:begin:T:N:CKSUM]]` / `[[LG:T:NNNN]]<chunk>` / `[[LG:end:T:CKSUM]]`. |
| `SyncPayloadCache.cs` | `SyncPayloadCache` | Builds `TierBlobData[]` per tier. JSON → GZip → base64 → 440-char chunks. Critical always built; High/Normal only if data exists. `GetAllTierBlobs()` returns cached array O(1). `Rebuild()` called twice at startup. |

### Services/

| File | Class | Purpose |
|------|-------|---------|
| `PrefabNameResolver.cs` | `PrefabNameResolver` | Scans LilithsMind definitions via reflection. Builds `_nameToGuid`, `_prefabToGuid`, `_guidToName`. Provides `TryResolve()`, `TryResolveName()`. |
| `HeartConfigBuilder.cs` | `HeartConfigBuilder` | Example config file generation. `GenerateIfRequested()` called by `Heart.OnInitialize()` before `LocalizationService` loads. Writes `Items/example.json` demonstrating all three icon methods. Flag resets to false after generation. Future generation flags for MainQuest/, Spells/ go here. |
| `LocalizationService.cs` | `LocalizationService` | Central localization loader — **loader only, no file writing**. Multiple registered directories via `RegisterDirectory()`. Each dir scanned recursively for `*.json`, merged alphabetically into `ItemAppearanceConfig`. Supports `Reload()`. |

### Config/

| File | Class | Purpose |
|------|-------|---------|
| `HeartConfig.cs` | `HeartConfig` | `DebugLogging` (bool), `ServerName` (string), `GenerateExampleConfigs` (bool — replaces `GenerateLocalizationExample`, triggers all registered module generators), `ChunksPerFrame` (int). |
| `HeartPathIndex.cs` | `HeartPathIndex` | `Root`, `CoreConfig`, `ItemsDir` (replaces `LocalizationDir`), `ModuleConfig()`, `DataDir()`. |
| `ItemAppearanceConfig.cs` | `ItemAppearanceConfig` | Pure data surface — `Dictionary<string, ItemAppearanceData>`. Per-field merge via `AddOverride()` (later file wins per field, not per entry). `Clear()`, `MarkLoaded()`. |

---

## LilithsCookbook (Server Plugin)

### Root

| File | Class | Purpose |
|------|-------|---------|
| `CookbookPlugin.cs` | `CookbookPlugin : BasePlugin` | BepInEx entry point. Loads config, registers with HeartModuleRegistry, subscribes to `Heart.OnInitialized`. |
| `LilithsCookbook.csproj` | — | Net6.0, references Heart + Mind, VampireReferenceAssemblies, VCF |

### Systems/

| File | Class | Purpose |
|------|-------|---------|
| `RecipeSystem.cs` | `RecipeSystem` | Applies recipe changes to server ECS. Builds `LilithRecipeData` overrides for Soul sync. |
| `StationSystem.cs` | `StationSystem` | Two-pass: patch prefab entities, then patch live User + placed station entities after `RegisterGameData()`. |
| `CookbookLoader.cs` | `CookbookLoader` | Reads and merges `*.json` from Recipes/ and Stations/. Later files win. |
| `CookbookConfigBuilder.cs` | `CookbookConfigBuilder` | Example config generation. Vanilla recipe dump if `GenerateAllRecipes` enabled. Renamed from `CookbookBuilder` to match `*ConfigBuilder` convention. |

### Data/

| File | Class | Purpose |
|------|-------|---------|
| `CookbookItemData.cs` | `CookbookItemData` | `Item` (string) + `Amount` (int). |
| `CookbookRecipeData.cs` | `CookbookRecipeData`, `RecipeEntryData` | JSON-deserializable recipe config DTOs. |
| `CookbookStationData.cs` | `CookbookStationData`, `StationEntryData` | JSON-deserializable station config DTOs. |

### Config/

| File | Class | Purpose |
|------|-------|---------|
| `CookbookConfig.cs` | `CookbookConfig` | `GenerateAllRecipes` (bool) with auto-reset. |

---

## LilithsSoul (Client Plugin)

### Root

| File | Class | Purpose |
|------|-------|---------|
| `SoulPlugin.cs` | `SoulPlugin : BasePlugin` | BepInEx entry point. Calls `SoulCoroutineHost.Register()`, loads config, applies Harmony patches. |
| `LilithsSoul.csproj` | — | Net6.0, references Mind, VRising.Unhollowed.Client |

### Foundation/

| File | Class | Purpose |
|------|-------|---------|
| `Soul.cs` | `Soul` | Client world access, `EntityManager` accessor, `Reset()` for disconnect. |
| `SoulLogger.cs` | `SoulLogger` | Client logging wrapper. |
| `EntityExtensions.cs` | `EntityExtensions` | Fluent ECS extension methods using `Soul.EntityManager`. |
| `SoulCoroutineHost.cs` | `SoulCoroutineHost` | IL2CPP `MonoBehaviour` coroutine host. Required by `IconDownloader` for async `UnityWebRequest` downloads. Registered via `ClassInjector.RegisterTypeInIl2Cpp` in `SoulPlugin.Load()`. Lazily creates a persistent `GameObject` on first `Run()` call. |

### Services/

| File | Class | Purpose |
|------|-------|---------|
| `LocalizationPatcher.cs` | `LocalizationPatcher` | **Replaces `LocalizationInjector` (retired).** Repoints item **display names**: per `ItemAppearanceOverrides` entry with a `DisplayName`, resolves prefab name → PrefabGUID (LilithsMind reflection), mints a fresh `AssetGuid` (`AssetGuid.FromString(Guid.NewGuid())`), writes the string to `Localization._LocalizedStrings`, and points the value-type `ManagedItemData.Name` at it. Captures originals for `ClearPrevious()` restore; **no `LoadDefaultLanguage`** (which previously wiped minted keys). Tooltips are NOT handled — `ManagedItemData.Description` is a copy-returning reference property; pending a Harmony patch. Does **not** require recorded `PrefabDef.NameKey`. |
| `IconPatcher.cs` | `IconPatcher` | Applies `Icon` from `payload.ItemAppearanceOverrides` to `ManagedItemData.Icon`. Builds at world ready: prefab name → PrefabGUID (LilithsMind reflection), filename → PNG path (Icons/ recursive scan, PNG only), sprite name → Sprite (Resources). Resolution order: local file → in-game sprite → https:// URL. Stores previous icons for `ClearPrevious()` restore. |
| `IconDownloader.cs` | `IconDownloader` | https:// URL icon downloads. Checks Icons/ cache first. Downloads via `UnityWebRequestTexture`, saves as PNG, invokes callback. Runs via `SoulCoroutineHost`. Filename derived from URL last path segment. |
| `RecipePatcher.cs` | `RecipePatcher` | Name→GUID map from PrefabCollectionSystem + LilithsMind. Patches RecipeData, RecipeHashLookupMap, buffers, WorkstationRecipesBuffer. |
| `ServerRegistry.cs` | `ServerRegistry` | `servers.json` — maps connection string → folder name. `Load()`, `TryGetFolderName()`, `Register()`. |

### Patches/

| File | Class | Purpose |
|------|-------|---------|
| `ClientInitPatch.cs` | `ClientInitPatch` | Harmony postfix on `GameDataManager.OnUpdate`. Single-fire — reads `ClientBootstrapSystem.ConnectionString`, calls `SyncReceiver.NotifyWorldReady()`. |
| `ClientChatSystemPatch.cs` | `ClientChatSystemPatch` | Harmony **prefix** on `ClientChatSystem.OnUpdate`. Filters `ServerChatMessageType.System`, passes to `SyncReceiver.TryHandleMessage()`. Destroys consumed entities. |

### Network/

| File | Class | Purpose |
|------|-------|---------|
| `SyncReceiver.cs` | `SyncReceiver` | Accumulates tiered chunks. On `[[LG:end:T:CKSUM]]`: base64-decode, GZip-decompress, deserialize, write to disk, apply. `NotifyWorldReady()` calls `LocalizationPatcher.BuildNameMap()`, `RecipePatcher.BuildNameMap()`, `IconPatcher.BuildSpriteMaps()`. `ApplyPayload` order: LocalizationPatcher (ClearPrevious→Apply) → IconPatcher (ClearPrevious→Apply) → RecipePatcher. |

### Config/

| File | Class | Purpose |
|------|-------|---------|
| `SoulConfig.cs` | `SoulConfig` | `DebugLogging` (bool). |
| `SoulPathIndex.cs` | `SoulPathIndex` | `Root`, `CoreConfig`, `IconsDir` (Icons/ for PNG files + URL cache), `ServerDir()`, `SyncFile()`. |

---

## Changelog (this session)

### Renamed
- `CookbookBuilder` → `CookbookConfigBuilder` — matches `*ConfigBuilder` convention

### Added
- `LilithsMind/Data/ItemAppearanceData.cs` — `ItemAppearanceData` DTO
- `LilithsHeart/Services/HeartConfigBuilder.cs` — `HeartConfigBuilder` — example config generation, extracted from `LocalizationService`
- `LilithsHeart/Network/SyncTierEnum.cs` — `SyncTierEnum` — 5 priority tiers
- `LilithsHeart/Network/TierBlobData.cs` — `TierBlobData` — pre-built chunk data per tier
- `LilithsHeart/Network/SyncQueue.cs` — `SyncQueue` — controlled rate delivery queue
- `LilithsHeart/Patches/SchedulerPatch.cs` — `SchedulerPatch` — per-frame queue drain
- `LilithsSoul/Foundation/SoulCoroutineHost.cs` — `SoulCoroutineHost` — IL2CPP coroutine host
- `LilithsSoul/Services/IconPatcher.cs` — `IconPatcher` — icon override application
- `LilithsSoul/Services/IconDownloader.cs` — `IconDownloader` — https:// URL download + cache

### Modified
- `LilithsMind/Network/ServerSyncPayload.cs` — `DisplayNameOverrides`/`TooltipOverrides` → `ItemAppearanceOverrides: Dictionary<string, ItemAppearanceData>`
- `LilithsHeart/Config/HeartPathIndex.cs` — `LocalizationDir` → `ItemsDir`
- `LilithsHeart/Config/ItemAppearanceConfig.cs` — two flat dicts → single `Dictionary<string, ItemAppearanceData>` with per-field merge
- `LilithsHeart/Services/LocalizationService.cs` — registered directories, recursive scan, `EnsureExampleFile` removed (→ `HeartConfigBuilder`)
- `LilithsHeart/Network/SyncPayloadCache.cs` — `CachedJson` → `TierBlobData[]`, GZip+base64 compression per tier
- `LilithsSoul/Config/SoulPathIndex.cs` — added `IconsDir`
- `LilithsSoul/Services/LocalizationInjector.cs` — reads from `ItemAppearanceOverrides`
- `LilithsSoul/Network/SyncReceiver.cs` — added `IconPatcher` calls
- `LilithsSoul/SoulPlugin.cs` — added `SoulCoroutineHost.Register()`
- `LilithsHeart/Foundation/Heart.cs` — added `HeartConfigBuilder.GenerateIfRequested()`
- `LilithsCookbook/Services/CookbookConfigBuilder.cs` — renamed from `CookbookBuilder`

### Added (LocalizationPatcher fold-in)
- `LilithsSoul/Services/LocalizationPatcher.cs` — `LocalizationPatcher` — repoints item display names via minted AssetGuids; replaces LocalizationInjector for the name path. Tooltip handling deferred (Harmony patch, separate task).

### Removed (LocalizationPatcher fold-in)
- `LilithsSoul/Services/LocalizationInjector.cs` — retired. Overwrote shared localization keys and reloaded the table via `LoadDefaultLanguage()` on clear, wiping its own writes on the second apply (renames reverted to raw GUIDs).
- `LilithsSoul/Services/RepointDiagnostic.cs` — temporary investigation probe; deleted after confirming the repoint mechanism.

### Modified (LocalizationPatcher fold-in)
- `LilithsSoul/Network/SyncReceiver.cs` — `ApplyPayload` swaps `LocalizationInjector.Inject` for `LocalizationPatcher.ClearPrevious()` + `LocalizationPatcher.Apply()`; `NotifyWorldReady` builds the patcher's name map. FIXED order is now 7 steps (localization clear+apply, icon clear+apply, recipes).
- `LilithsSoul/Patches/ClientInitPatch.cs` — removed temporary probe/test call; back to `SyncReceiver.NotifyWorldReady()` only.
- `PrefabDef` usage — `NameKey` no longer required for naming (see PrefabDef row).