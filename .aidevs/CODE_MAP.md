# Code Map — File-by-File Index

## Root Files

| File | Purpose |
|------|---------| 
| `LilithsEngine.sln` | Visual Studio solution referencing 4 projects |
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
| `LilithItemData.cs` | `LilithItemData` | Unified item override DTO. Fields in order: `DisplayName?`, `DescriptionText?`, `Icon?`, `ChangesEnabled` (bool, gates functional fields), `StackSize?` (int). Appearance fields always apply when non-null. `ChangesEnabled` gates StackSize and future functional fields. Server-side StackSize never synced to Soul — filtered in SyncPayloadCache. |
| `LanguageCodeEnum.cs` | `LanguageCodeEnum` | V Rising / Steam language codes: English, Brazilian, French, German, Hungarian, Italian, Japanese, Koreana, Latam, Polish, Russian, SChinese, Spanish, TChinese, Thai, Turkish, Ukrainian, Vietnamese, Custom. Folder names under Localization/ must match exactly. |
| `SyncModeEnum.cs` | `SyncModeEnum` | Sync transport mode: `ChunkPush` (default), `HttpServer`, `StaticUrl`. Configured in HeartConfig. |
| `SyncTierEnum.cs` | `SyncTierEnum` | Canonical 0-based tier enum. `Critical(0)`, `High(1)`, `Normal(2)`, `Low(3)`, `Background(4)`. Single source of truth for both Heart and Soul. |

### Prefabs/

| File | Class | Purpose |
|------|-------|---------|
| `PrefabDef.cs` | `PrefabDef` | `readonly record struct` — universal prefab definition (Name, GuidHash, Prefab, NameKey, DescKey). Stack-allocated, zero heap pressure. `NameKey`/`DescKey` retained as vanilla reference only — not required for appearance overrides. |

### Prefabs/Definitions/ — 22 static index classes

| File | Contains |
|------|----------|
| `WeaponsIndex.cs` | All weapon items |
| `StationsIndex.cs` | All crafting/refinement station prefabs |
| `ArmorHeadIndex.cs` | Helmet/head armor prefabs |
| `ArmorChestIndex.cs` | Chest armor prefabs |
| `ArmorLegsIndex.cs` | Leg armor prefabs |
| `ArmorGlovesIndex.cs` | Glove armor prefabs |
| `ArmorBootsIndex.cs` | Boot armor prefabs |
| `ArmorCloakIndex.cs` | Cloak prefabs |
| `AccessoryIndex.cs` | Rings, sources, necklaces |
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
| `ServerSyncPayload.cs` | `ServerSyncPayload` | Full data contract: `ServerIdentity`, `PayloadHash`, `ServerLanguage` (default "English"), `ItemAppearanceOverrides: Dictionary<string, LilithItemData>` (appearance fields only — StackSize filtered out by SyncPayloadCache), `RecipeOverrides`, `StationRecipeOverrides`, `PlayerRecipesToAdd`, `PlayerRecipesToRemove`. |
| `TierBlobData.cs` | `TierBlobData` | Pre-built chunk data for one tier: `Tier`, `Chunks[]` (base64+gzip strings), `ChunkCount`, `Checksum`. Immutable after construction. |
| `ServerEventPayload.cs` | `ServerEventPayload`, `EventKind` | Trigger-based in-session payload. Reserved — not yet implemented. |

---

## LilithsHeart (Server Plugin)

### Root

| File | Class | Purpose |
|------|-------|---------|
| `HeartPlugin.cs` | `HeartPlugin : BasePlugin` | BepInEx entry point. Initializes logger, config, event bus, module registry, Harmony patches. |
| `LilithsHeart.csproj` | — | Net6.0, references Mind, VRising.Unhollowed.Client, VCF. EmbeddedResource entries for Resources/Examples/ and Resources/Debug/ JSON files. |

### Resources/Examples/

Embedded JSON files extracted on demand when GenerateHeartExamples or GenerateAllModuleExamples is set.

| File | Purpose |
|------|---------|
| `Examples_Item.json` | Item appearance example (DisplayName, DescriptionText, Icon). Shows all three icon resolution methods. ChangesEnabled=false. |

### Resources/Debug/

Embedded JSON files extracted on demand when GenerateDebugConfigs is set.

| File | Purpose |
|------|---------|
| `Debug_Item.json` | Item appearance debug (DEBUG_ prefixed names, Icon_BloodOrb swap). No PNG needed. ChangesEnabled=false (appearance has no gate). |

### Foundation/

| File | Class | Purpose |
|------|-------|---------|
| `Heart.cs` | `Heart` | Server world access, ECS system accessors, module registration API (`RegisterRecipeOverrides`, `RegisterStationRecipeChanges`, `RegisterPlayerRecipeChanges`). Starts/stops `SyncHttpServer` when mode is HttpServer. Fires `OnInitialized` and `OnWorldReady`. |
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

### Config/

| File | Class | Purpose |
|------|-------|---------|
| `HeartConfig.cs` | `HeartConfig` | `ServerName`, `ChunksPerFrame`, `DefaultLanguage` (LanguageCodeEnum), `DebugLogging`, `SyncMode` (SyncModeEnum), `HttpPort`, `StaticSyncUrl`, `SyncFallbackToChunks`, `GenerateHeartExamples`, `GenerateAllModuleExamples`, `GenerateDebugConfigs`, `GenerateNameAliasConfigs`. Each generation flag has a corresponding `Disable*()` method. |
| `HeartPathIndex.cs` | `HeartPathIndex` | `Root`, `CoreConfig`, `AliasesDir`, `ItemsDir`, `LocalizationDir`, `ModuleConfig()`, `DataDir()`. |
| `LilithItemConfig.cs` | `LilithItemConfig` | Pure data surface — `Overrides: IReadOnlyDictionary<string, LilithItemData>`. Single dictionary for all item overrides (appearance + functional). `AddOverride()` does per-field merge. `Clear()`, `MarkLoaded()`, `GetOverride()`. |

### Patches/

| File | Class | Purpose |
|------|-------|---------|
| `InitializationPatch.cs` | `InitializationPatch` | Harmony postfix on `WarEventRegistrySystem.RegisterWarEventEntities`. Single-fire — calls `Heart.OnInitialize()`. |
| `ClientConnectPatch.cs` | `ClientConnectPatch` | Harmony postfix on `ServerBootstrapSystem.OnUserConnected`. Resolves User + Character entities + userIndex. Branches on `HeartConfig.SyncMode` — `EnqueueSyncTiers()` for ChunkPush, `SendRedirect()` for HttpServer/StaticUrl. |
| `SchedulerPatch.cs` | `SchedulerPatch` | Harmony postfix on `ServerBootstrapSystem.OnUpdate`. Per-frame drain of `SyncQueue` at `ChunksPerFrame` rate. Fast-path: single `HasPending` bool check when idle. |
| `ServerChatSystemPatch.cs` | `ServerChatSystemPatch` | Harmony postfix on `ServerBootstrapSystem.OnUpdate`. General Soul→Heart sentinel intercept. Queries `ChatMessageServerEvent + FromCharacter` entities. Handles `[[LG:sync-fallback]]` (enqueues chunks for that client) and `[[LG:lang-request:X]]` (triggers LocalizationSyncSender). The single home for all Soul→Heart communication. |

### Network/

| File | Class | Purpose |
|------|-------|---------|
| `SyncQueue.cs` | `SyncQueue` | Thread-safe FIFO queue of pending client sends. `Enqueue()` at connect captures NetworkIds (entities valid then). `Drain()` guards each entry with `em.Exists(UserEntity)`. |
| `SyncSender.cs` | `SyncSender` | `EnqueueSyncTiers()` builds tier messages, enqueues into SyncQueue (ChunkPush mode). `SendRedirect()` sends `[[LG:sync-url:<url>:<fallback>]]` sentinel (HttpServer/StaticUrl modes). Protocol: `[[LG:begin:T:N:CKSUM]]` / `[[LG:T:NNNN]]<chunk>` / `[[LG:end:T:CKSUM]]`. |
| `SyncPayloadCache.cs` | `SyncPayloadCache` | Builds `TierBlobData[]` per tier. Filters StackSize out of appearance payload (server-only). Populates `ServerLanguage` from `HeartConfig.DefaultLanguage`. Calls `SyncHttpServer.UpdatePayload()` after rebuild when mode is HttpServer. `Rebuild()` called twice at startup. |
| `SyncHttpServer.cs` | `SyncHttpServer` | HttpListener on background thread (HttpServer mode only). Serves `GET /sync` → current payload JSON. `Start()`/`Stop()` lifecycle. `UpdatePayload()` called by SyncPayloadCache after each rebuild. Port configured via `HeartConfig.HttpPort`. |
| `LocalizationSyncSender.cs` | `LocalizationSyncSender` | Handles language requests from Soul. Builds a ServerSyncPayload with only DisplayName/DescriptionText for the requested language (via LocalizationFileService), enqueues as Critical-tier chunks. Sends `[[LG:lang-unavailable:X]]` if language not configured. |

### Services/

| File | Class | Purpose |
|------|-------|---------|
| `PrefabNameResolver.cs` | `PrefabNameResolver` | Three-path lookup. Phase 1: scans LilithsMind via reflection → `_nameToGuid`, `_prefabToGuid`, `_guidToName`, `_hashToGuid`, `_entriesByIndexClass`. Phase 2: loads Aliases/*.json admin overrides. `TryResolve()` checks: (1) alias/Name, (2) prefab string, (3) raw GuidHash integer. `TryResolveName()` reverse lookup. `GenerateAliasFiles()` dumps compiled defaults to Aliases/. |
| `HeartConfigBuilder.cs` | `HeartConfigBuilder` | Coordinates all suite config generation. `RunIfRequested()` checks all generation flags. Extracts embedded JSON resources (Items/Examples_Item.json, Items/Debug_Item.json). `RegisterExampleGenerator()` / `RegisterDebugGenerator()` — module registration. Calls registered generators after writing Heart's own files. `ExtractResource()` helper for embedded resource extraction. |
| `ItemService.cs` | `ItemService` | Single owner of all Items/*.json file I/O. `RegisterDirectory()`, `Initialize()`, `Reload()`. Parses all fields (DisplayName, DescriptionText, Icon, ChangesEnabled, StackSize) into `LilithItemConfig` in one pass. Per-field merge, alphabetical order, later files win. `Reload()` triggers `Heart.OnLocalizationReloaded()`. |
| `LocalizationService.cs` | `LocalizationService` | Pure apply-layer diagnostic. `Initialize()` logs DisplayName/DescriptionText entry counts from `LilithItemConfig`. No file I/O — all loading done by ItemService. |
| `InterfaceService.cs` | `InterfaceService` | Pure apply-layer diagnostic. `Initialize()` logs Icon entry counts from `LilithItemConfig`. No file I/O. |
| `LocalizationFileService.cs` | `LocalizationFileService` | Loads per-language DisplayName/DescriptionText overrides from `Localization/<LanguageCode>/` subdirs. Validates folder names against `LanguageCodeEnum`. `HasLanguage()`, `AvailableLanguages`. `BuildLocalizationPayload()` returns a ServerSyncPayload with only the localization slice (no Icon, no StackSize). |

---

## LilithsCookbook (Server Plugin)

### Root

| File | Class | Purpose |
|------|-------|---------|
| `CookbookPlugin.cs` | `CookbookPlugin : BasePlugin` | BepInEx entry point. Checks `ModuleEnabled` immediately after config init — returns early if false (no ECS work, no registration). Registers example/debug generators with HeartConfigBuilder. Subscribes to `Heart.OnInitialized`. |
| `LilithsCookbook.csproj` | — | Net6.0, references Heart + Mind, VampireReferenceAssemblies. EmbeddedResource entries for Resources/Examples/ and Resources/Debug/ JSON files. |

### Resources/Examples/

| File | Purpose |
|------|---------|
| `Examples_Recipes.json` | 3 recipe override examples. ChangesEnabled=false. |
| `Examples_PrisonerFeedRecipes.json` | 2 prisoner feed recipe examples (one standard, one with FakeItem + item output). |
| `Examples_PrisonerFeedItems.json` | 3 FakeItem examples (FeedPrisoner, DealDamageToPrisoner, AffectWithToxic). |
| `Examples_CookbookItems.json` | 3 StackSize examples. ChangesEnabled=false. |

### Resources/Debug/

| File | Purpose |
|------|---------|
| `Debug_Recipes.json` | 3 recipe debug entries. ChangesEnabled=true, CraftDuration=1. |
| `Debug_PrisonerFeedRecipes.json` | Feed recipe durations cut to 1 second. |
| `Debug_PrisonerFeedItems.json` | Extreme stat values for obvious in-game verification. |
| `Debug_CookbookItems.json` | StackSize=9999 entries. ChangesEnabled=true. |

### Config/

| File | Class | Purpose |
|------|-------|---------|
| `CookbookConfig.cs` | `CookbookConfig` | `ModuleEnabled`, `GenerateAllRecipes`, `GenerateCookbookExamples`, `GenerateCookbookDebugConfigs`. Each generation flag has a `Disable*()` method. |

### Data/

| File | Class | Purpose |
|------|-------|---------|
| `CookbookItemData.cs` | `CookbookItemData` | `Item` (string) + `Amount` (int). Used for recipe requirements, outputs, repair costs, unit outputs. |
| `CookbookRecipeData.cs` | `CookbookRecipeData`, `RecipeEntryData`, `PrisonerFeedEntryData` | JSON-deserializable recipe and prisoner feed config DTOs. `RecipeEntryData` has scalar fields + buffer lists. `PrisonerFeedEntryData` has `Type` (PrisonerFeedTypeEnum) + type-specific stat fields. |
| `CookbookPrisonerFeedData.cs` | `PrisonerFeedTypeEnum` | `FeedPrisoner`, `DealDamageToPrisoner`, `AffectWithToxic`. |

### Services/

| File | Class | Purpose |
|------|-------|---------|
| `CookbookConfigBuilder.cs` | `CookbookConfigBuilder` | Extracts all Cookbook config files from embedded resources. `Initialize()` creates directories. `GenerateExampleFiles()` extracts 4 example files. `GenerateDebugFiles()` extracts 4 debug files. `GenerateAllRecipesIfRequested()` dumps vanilla ECS recipe data to AllRecipes.json. |
| `CookbookLoader.cs` | `CookbookLoader` | Reads and merges `*.json` from Recipes/. Later files win per-field. |
| `ItemFunctionService.cs` | `ItemFunctionService` | Patches `ItemData.MaxAmount` on item prefab entities for StackSize overrides. Reads from `LilithItemConfig.Overrides` — only applies entries where `ChangesEnabled=true`. Called from `CookbookPlugin.OnHeartInitialized()`. Server-side only — StackSize never synced to Soul. |

### Systems/

| File | Class | Purpose |
|------|-------|---------|
| `RecipeSystem.cs` | `RecipeSystem` | Applies recipe changes to server ECS. Patches `RecipeData`, `RecipeHashLookupMap`, `RecipeRequirementBuffer`, `RecipeOutputBuffer`. Builds `LilithRecipeData` overrides for Soul sync. |
| `StationSystem.cs` | `StationSystem` | Two-pass patching: Pass 1 patches prefab entities → `RegisterRecipes()` + `RegisterGameData()` → Pass 2 patches live placed station entities. Uses `GetAllEntities()` with direct prefab-entity identity exclusion (placed world instances retain `Unity.Entities.Prefab` tag — `None=[Prefab]` query exclusion is ineffective). |
| `PrisonerFeedSystem.cs` | `PrisonerFeedSystem` | Patches FakeItem prefab entities for prisoner feeding stat overrides. Three ECS component types: `FeedPrisoner` (health/misery/blood quality), `AffectPrisonerWithToxic` (mutation chance/blood quality), `DealDamageToPrisoner` (damage/torture). |

---

## LilithsSoul (Client Plugin)

### Root

| File | Class | Purpose |
|------|-------|---------|
| `SoulPlugin.cs` | `SoulPlugin : BasePlugin` | BepInEx entry point. Calls `SoulCoroutineHost.Register()`, loads config, applies Harmony patches. |
| `LilithsSoul.csproj` | — | Net6.0, references Mind, VRising.Unhollowed.Client. |

### Foundation/

| File | Class | Purpose |
|------|-------|---------|
| `Soul.cs` | `Soul` | Client world access, `EntityManager` accessor, `Reset()` for disconnect. |
| `SoulLogger.cs` | `SoulLogger` | Client logging wrapper. |
| `EntityExtensions.cs` | `EntityExtensions` | Fluent ECS extension methods using `Soul.EntityManager`. |
| `SoulCoroutineHost.cs` | `SoulCoroutineHost` | IL2CPP `MonoBehaviour` coroutine host. Required by `IconDownloader` and `SyncHttpFetcher` for async operations. `Run()` starts a coroutine. Registered via `ClassInjector.RegisterTypeInIl2Cpp` in `SoulPlugin.Load()`. |

### Config/

| File | Class | Purpose |
|------|-------|---------|
| `SoulConfig.cs` | `SoulConfig` | `DebugLogging` (bool), `PreferredLanguage` (LanguageCodeEnum, default English). |
| `SoulPathIndex.cs` | `SoulPathIndex` | `Root`, `CoreConfig`, `IconsDir`, `ServerDir()`, `SyncFile()`, `LocalizationFile(serverIdentity, languageName)`. |

### Services/

| File | Class | Purpose |
|------|-------|---------|
| `LocalizationPatcher.cs` | `LocalizationPatcher` | Repoints item display names. Mints fresh `AssetGuid` per item, injects into `Localization._LocalizedStrings`, sets `ManagedItemData.Name`. `ClearPrevious()` restores originals. `BuildNameMap()` via LilithsMind reflection. |
| `DescriptionPatcher.cs` | `DescriptionPatcher` | Repoints item descriptions (tooltip body). Same mint/inject mechanism as LocalizationPatcher. Mandatory struct write-back: `var d = item.Description; d.Key = mintedKey; item.Description = d;` (`LocalizedStringBuilderBase` is a value-type struct). `Clear()` restores originals. |
| `IconPatcher.cs` | `IconPatcher` | Applies Icon overrides to `ManagedItemData.Icon`. Resolution order: (1) https:// URL → IconDownloader, (2) local PNG → `_localFiles` recursive scan, (3) in-game sprite → `_gameSprites`. `TryGetLocalFile()` exposed for IconDownloader cache check. `ClearPrevious()` restores originals. |
| `IconDownloader.cs` | `IconDownloader` | https:// URL icon downloads. Cache check via `IconPatcher.TryGetLocalFile()` (covers all Icons/ subdirectories). On cache miss: downloads via `UnityWebRequestTexture`, saves to Icons/ root as PNG. Runs via `SoulCoroutineHost.Run()`. |
| `RecipePatcher.cs` | `RecipePatcher` | Patches client-side recipe ECS entities to match server overrides. `BuildNameMap()` from PrefabCollectionSystem + LilithsMind. `Apply()`, `ApplyStationRecipes()`, `ApplyPlayerRecipes()`. |
| `ServerRegistry.cs` | `ServerRegistry` | `servers.json` — maps connection string → server identity folder name. `Load()`, `TryGetFolderName()`, `Register()`. |

### Patches/

| File | Class | Purpose |
|------|-------|---------|
| `ClientInitPatch.cs` | `ClientInitPatch` | Harmony postfix on `GameDataManager.OnUpdate`. Single-fire — reads `ClientBootstrapSystem.ConnectionString`, calls `SyncReceiver.NotifyWorldReady()`. |
| `ClientChatSystemPatch.cs` | `ClientChatSystemPatch` | Harmony prefix on `ClientChatSystem.OnUpdate`. Filters `ServerChatMessageType.System`, passes to `SyncReceiver.TryHandleMessage()`. Destroys consumed entities before UI sees them. |

### Network/

| File | Class | Purpose |
|------|-------|---------|
| `SyncReceiver.cs` | `SyncReceiver` | Intercepts and reassembles tiered sync payload. Handles: `[[LG:sync-url:...]]` (redirect → SyncHttpFetcher), `[[LG:lang-unavailable:X]]` (log warning), `[[LG:begin/end/chunk]]` (ChunkPush protocol). On tier complete: verify SHA256 → decompress → deserialize → cache to disk → `ApplyTier()`. Language request logic: compares `SoulConfig.PreferredLanguage` vs `payload.ServerLanguage` on Critical tier receipt, sends `[[LG:lang-request:X]]` if different. Pre-apply: `TryPreApplyCachedSync()` + `TryPreApplyCachedLocalization()` in `NotifyWorldReady()`. Fallback: `SendFallbackSentinel()` creates `ChatMessageEvent { MessageType = Local }` in client ECS world. |
| `SyncHttpFetcher.cs` | `SyncHttpFetcher` | Fetches sync payload from URL via `UnityWebRequest` coroutine (10s timeout). On success: invokes onSuccess callback. On failure: invokes onFailure. Runs via `SoulCoroutineHost.Run()`. No `using` on `UnityWebRequest` (not IDisposable in IL2CPP). |