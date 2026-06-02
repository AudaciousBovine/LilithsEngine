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
| `ItemAppearanceData.cs` | `ItemAppearanceData` | DTO with optional `DisplayName`, `DescriptionText`, `Icon` fields. Value type in `ServerSyncPayload.ItemAppearanceOverrides`. **`DescriptionText` was formerly `Tooltip`** (renamed; no back-compat shim). Icon value is self-describing: filename → local PNG, sprite name → in-game sprite, https:// → URL download. |

### Prefabs/

| File | Class | Purpose |
|------|-------|---------|
| `PrefabDef.cs` | `PrefabDef` | `readonly record struct` — universal prefab definition (Name, GuidHash, Prefab, NameKey, DescKey). Stack-allocated, zero heap pressure. **`NameKey`/`DescKey` are no longer required for any appearance override** — both names and descriptions mint fresh keys and repoint the live value-type key by PrefabGUID. The fields are retained only as a vanilla-key reference record. |

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
| `ServerSyncPayload.cs` | `ServerSyncPayload` | Full data contract: identity, hash, `ItemAppearanceOverrides: Dictionary<string, ItemAppearanceData>`, recipe overrides, station overrides, player recipe changes. |
| `SyncTierEnum.cs` | `SyncTierEnum` | **Canonical 0-based tier enum (moved here from Heart).** `Critical(0)`, `High(1)`, `Normal(2)`, `Low(3)`, `Background(4)`. Single source of truth for both Heart and Soul. |
| `TierBlobData.cs` | `TierBlobData` | **Moved here from Heart this session.** Pre-built chunk data for one tier: `Tier`, `Chunks[]` (base64+gzip strings), `ChunkCount`, `Checksum`. Immutable after construction. (Header comment may still read "LilithsHeart" — cosmetic.) |
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
| `SyncQueue.cs` | `SyncQueue` | Thread-safe FIFO queue of pending client sends. `Enqueue()` on connect (captures user/character NetworkId AT ENQUEUE — entities are valid then), `Drain()` each frame guards each entry with `em.Exists(UserEntity)` and drops+logs disconnected clients. `ChunksPerFrame = 10`. |
| `SyncSender.cs` | `SyncSender` | `EnqueueSyncTiers()` builds tier messages from `TierBlobData`, enqueues into `SyncQueue`. `SendQueuedChunk()` creates one `ChatMessageServerEvent` entity with `SendEventToUser`. Protocol: `[[LG:begin:T:N:CKSUM]]` / `[[LG:T:NNNN]]<chunk>` / `[[LG:end:T:CKSUM]]`. |
| `SyncPayloadCache.cs` | `SyncPayloadCache` | Builds `TierBlobData[]` per tier. `BuildBlob`: JSON → GZip → `Convert.ToBase64String` (whole blob base64'd ONCE) → sliced into 440-char chunks. Checksum = `SHA256` over the base64 TEXT, uppercase first 8 hex. Critical always built; High/Normal only if data exists. `GetAllTierBlobs()` O(1). `Rebuild()` called twice at startup. |

### Services/

| File | Class | Purpose |
|------|-------|---------|
| `PrefabNameResolver.cs` | `PrefabNameResolver` | Scans LilithsMind definitions via reflection. Builds `_nameToGuid`, `_prefabToGuid`, `_guidToName`. Provides `TryResolve()`, `TryResolveName()`. |
| `HeartConfigBuilder.cs` | `HeartConfigBuilder` | Example config file generation. `GenerateIfRequested()` called by `Heart.OnInitialize()` before `LocalizationService` loads. Writes `Items/example.json` demonstrating all three icon methods and a `DescriptionText` example. Skips if `example.json` already exists. Flag resets to false after generation. |
| `LocalizationService.cs` | `LocalizationService` | Central localization loader — **loader only, no file writing**. Multiple registered directories via `RegisterDirectory()`. Each dir scanned recursively for `*.json`, merged alphabetically into `ItemAppearanceConfig`. Supports `Reload()`. |

### Config/

| File | Class | Purpose |
|------|-------|---------|
| `HeartConfig.cs` | `HeartConfig` | `DebugLogging` (bool), `ServerName` (string), `GenerateExampleConfigs` (bool), `ChunksPerFrame` (int). |
| `HeartPathIndex.cs` | `HeartPathIndex` | `Root`, `CoreConfig`, `ItemsDir`, `ModuleConfig()`, `DataDir()`. |
| `ItemAppearanceConfig.cs` | `ItemAppearanceConfig` | Pure data surface — `Dictionary<string, ItemAppearanceData>`. **Renamed from `LocalizationConfig`.** Per-field merge via `AddOverride()` (later file wins per field, not per entry). `Clear()`, `MarkLoaded()`. |

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
| `StationSystem.cs` | `StationSystem` | Two-pass: patch prefab entities, then patch live placed station entities after `RegisterGameData()`. Uses `GetAllEntities()` with direct prefab-entity identity exclusion (the `None=[Prefab]` query exclusion is ineffective — placed world instances retain the `Unity.Entities.Prefab` tag). |
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
| `LilithsSoul.csproj` | — | Net6.0, references Mind, VRising.Unhollowed.Client. (Has a duplicate `LilithsMind` ProjectReference line — harmless; flagged for cleanup.) |

### Foundation/

| File | Class | Purpose |
|------|-------|---------|
| `Soul.cs` | `Soul` | Client world access, `EntityManager` accessor, `Reset()` for disconnect. |
| `SoulLogger.cs` | `SoulLogger` | Client logging wrapper. |
| `EntityExtensions.cs` | `EntityExtensions` | Fluent ECS extension methods using `Soul.EntityManager`. |
| `SoulCoroutineHost.cs` | `SoulCoroutineHost` | IL2CPP `MonoBehaviour` coroutine host. Required by `IconDownloader` for async `UnityWebRequest` downloads. Registered via `ClassInjector.RegisterTypeInIl2Cpp` in `SoulPlugin.Load()`. |

### Services/

| File | Class | Purpose |
|------|-------|---------|
| `LocalizationPatcher.cs` | `LocalizationPatcher` | Repoints item **display names**. Per `ItemAppearanceOverrides` entry with a `DisplayName`: resolve prefab name → PrefabGUID (LilithsMind reflection), mint a fresh `AssetGuid`, write the string to `Localization._LocalizedStrings`, and point the value-type `ManagedItemData.Name` at it. `ClearPrevious()` restores captured originals; no `LoadDefaultLanguage`. Does not require `PrefabDef.NameKey`. |
| `DescriptionPatcher.cs` | `DescriptionPatcher` | **Sole owner of item DESCRIPTION (tooltip body) overrides — added this session.** Data-layer repoint mirroring `LocalizationPatcher`. `BuildMap()` builds name/prefab → PrefabGUID. `Build(payload)` repoints each `DescriptionText`: mint `AssetGuid`, write string to `_LocalizedStrings`, then `var d = item.Description; d.Key = new LocalizationKey(guid); item.Description = d;` (struct write-back — `Description` is a value-type `LocalizedStringBuilderBase`). `Clear()` restores captured original Description structs. No UI patch. |
| `IconPatcher.cs` | `IconPatcher` | Applies `Icon` from `payload.ItemAppearanceOverrides` to `ManagedItemData.Icon`. Builds maps at world ready. Resolution order: local file → in-game sprite → https:// URL. `ClearPrevious()` restores. |
| `IconDownloader.cs` | `IconDownloader` | https:// URL icon downloads. Checks Icons/ cache first. Downloads via `UnityWebRequestTexture`, saves as PNG, invokes callback. Runs via `SoulCoroutineHost`. |
| `RecipePatcher.cs` | `RecipePatcher` | Name→GUID map from PrefabCollectionSystem + LilithsMind. Patches RecipeData, RecipeHashLookupMap, buffers, WorkstationRecipesBuffer. |
| `ServerRegistry.cs` | `ServerRegistry` | `servers.json` — maps connection string → folder name. `Load()`, `TryGetFolderName()`, `Register()`. |

### Patches/

| File | Class | Purpose |
|------|-------|---------|
| `ClientInitPatch.cs` | `ClientInitPatch` | Harmony postfix on `GameDataManager.OnUpdate`. Single-fire — reads `ClientBootstrapSystem.ConnectionString`, calls `SyncReceiver.NotifyWorldReady()`. |
| `ClientChatSystemPatch.cs` | `ClientChatSystemPatch` | Harmony **prefix** on `ClientChatSystem.OnUpdate`. Filters `ServerChatMessageType.System`, passes to `SyncReceiver.TryHandleMessage()`. Destroys consumed entities. |

> **Retired this session (deleted):** `ItemDescriptionPatch.cs` (the abandoned
> UI-patch approach to descriptions — every tooltip-build target crashed or
> never fired), and the temporary probes `DescriptionDataProbe.cs`,
> `TooltipStackProbe.cs`, `RepointDiagnostic.cs`. Descriptions are now handled
> entirely by `DescriptionPatcher` at the data layer. See DATA_FLOW "Why the
> description override is data-layer" for the failure record.

### Network/

| File | Class | Purpose |
|------|-------|---------|
| `SyncReceiver.cs` | `SyncReceiver` | Accumulates **tiered** chunks. Parses `[[LG:begin:T:N:CKSUM]]` / `[[LG:T:NNNN]]<data>` / `[[LG:end:T:CKSUM]]`. On end: concat chunk strings → SHA256-verify base64 text → base64-decode → GZip-decompress → deserialize → disk-merge → apply that tier immediately. World-ready deferral via `_pendingTierPayloads`. `NotifyWorldReady()` builds all patcher maps (Localization, Description, Recipe, Icon). `ApplyPayload`/per-tier order: Localization (ClearPrevious→Apply) → Description (Clear→Build) → Icon (ClearPrevious→Apply) → Recipe. |

### Config/

| File | Class | Purpose |
|------|-------|---------|
| `SoulConfig.cs` | `SoulConfig` | `DebugLogging` (bool). |
| `SoulPathIndex.cs` | `SoulPathIndex` | `Root`, `CoreConfig`, `IconsDir`, `ServerDir()`, `SyncFile()`. |

---

## Changelog (this session)

### Added
- `LilithsSoul/Services/DescriptionPatcher.cs` — `DescriptionPatcher` — item description (tooltip body) overrides via data-layer `LocalizationKey` repoint with struct write-back. The working description mechanism.

### Removed
- `LilithsSoul/Patches/ItemDescriptionPatch.cs` — the abandoned UI-patch approach. Every tooltip-build target either crashed when patched (`FakeTooltip.SetData`, `FakeTooltip.SetTooltip`) or never fired (`RefreshGeneralItemTooltip` ×2). Replaced by the data-layer repoint.
- `LilithsSoul/Services/DescriptionDataProbe.cs` — temporary read-only probe that found `ManagedItemData.Description`'s writable struct seam. Job done.
- `LilithsSoul/Patches/TooltipStackProbe.cs` — temporary probe on `FakeTooltip.SetData`; crashed (confirming the method is unpatchable). Job done.
- `LilithsSoul/Services/RepointDiagnostic.cs` — earlier name-repoint probe; deleted.

### Modified
- `LilithsMind/Data/ItemAppearanceData.cs` — field `Tooltip` → `DescriptionText`.
- `LilithsHeart/Config/ItemAppearanceConfig.cs` — renamed from `LocalizationConfig`.
- `LilithsHeart/Services/HeartConfigBuilder.cs` — `example.json` "Tooltip" key → "DescriptionText"; added description readme.
- `LilithsSoul/Network/SyncReceiver.cs` — rewritten to the tiered protocol (was a stale flat-protocol receiver that recognized no tiered sentinels — the cause of names/icons/descriptions all going dark); `ApplyPayload`/per-tier order now includes `DescriptionPatcher.Clear()` + `Build()` between the name and icon steps (9-step order).
- `LilithsSoul/Patches/ClientInitPatch.cs` — removed the probe `Run()` call; clean production.
- `LilithsSoul/Patches/ClientChatSystemPatch.cs` — removed temporary receive-path `[diag]` block.
- `LilithsHeart/Network/SyncQueue.cs`, `SyncSender.cs` — stale-Entity send fix: capture NetworkId at enqueue, guard each drain entry with `em.Exists(UserEntity)`.
- `LilithsMind/Network/SyncTierEnum.cs` — canonical 0-based enum consolidated here; the duplicate Heart copy and the dead 1-based `SyncTier`/`TierAssignments` variant removed.
- `LilithsMind/Network/TierBlobData.cs` — moved here from Heart (mirrors SyncTierEnum relocation).
- `PrefabDef` usage — `NameKey`/`DescKey` no longer required for any appearance override (both names and descriptions mint+repoint).