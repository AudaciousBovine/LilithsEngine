# Architecture

## Layer Diagram

```
LilithsMind (pure C#, no game deps)
    ├── Data/           — LilithItemData, PrefabDef, enums (LanguageCodeEnum, SyncModeEnum, SyncTierEnum)
    ├── Prefabs/        — PrefabDef + Definitions/*Index.cs static catalog
    ├── Network/        — ServerSyncPayload, LilithRecipeData, LilithStationData, TierBlobData
          │
          ▼
┌──────────────────────────────────────────────┐
│              LilithsHeart (server)             │
│  Foundation/  Events/  Config/  Services/     │
│  Network/     Patches/  Modules/  Resources/  │
│  Plugin entry: HeartPlugin.cs                  │
│  NuGet: VRising.Unhollowed.Client, VCF         │
└──────────────────────┬───────────────────────┘
                       │ BepInDependency
          ┌────────────┼────────────┐
          ▼            ▼            ▼
┌──────────────┐ ┌──────────────┐  (future child modules)
│LilithsCookbook│ │LilithsBounty │  LilithsArmory
│LilithsWisdom  │ │LilithsTreasury│  LilithsGrimoire
│LilithsConquest│ │LilithsBlessings│  LilithsAdversaries
│LilithsMenagerie│ │LilithsNexus  │  LilithsMachinations
│LilithsArchitects│ │             │  LilithsExpansion
└──────────────┘ └──────────────┘

LilithsSoul (client, standalone)
    Foundation/    Services/    Config/
    Network/       Patches/     UI/
    Plugin entry: SoulPlugin.cs
    NuGet: VRising.Unhollowed.Client
```

## Plugin GUIDs

| Plugin | GUID |
|--------|------|
| LilithsHeart | `audaciousbovine.lilithsheart` |
| LilithsCookbook | `audaciousbovine.lilithscookbook` |
| LilithsSoul | `audaciousbovine.lilithssoul` |

---

## Heart Initialization Sequence

```
HeartPlugin.Load()
  ├── HeartLogger.Initialize()
  ├── HeartConfig.Initialize()          — reads LilithsHeart.cfg
  ├── HeartEventBus.Initialize()
  ├── HeartModuleRegistry.Initialize()
  └── Harmony.PatchAll()
        │
        ▼  (BepInEx load order — child modules load after Heart)
CookbookPlugin.Load()
  ├── CookbookConfig.Initialize()       — reads LilithsCookbook.cfg
  ├── if (!ModuleEnabled) return        — early exit, no ECS work
  ├── CookbookConfigBuilder.Initialize() — creates Recipes/ and Items/ dirs
  ├── HeartConfigBuilder.RegisterExampleGenerator(CookbookConfigBuilder.GenerateExampleFiles)
  ├── HeartConfigBuilder.RegisterDebugGenerator(CookbookConfigBuilder.GenerateDebugFiles)
  └── Heart.OnInitialized += OnHeartInitialized
        │
        ▼  (world loads — WarEventRegistrySystem fires)
InitializationPatch.Postfix()
  └── Heart.OnInitialize()
        ├── PrefabNameResolver.Initialize()
        │     ├── Phase 1: Scans LilithsMind via reflection
        │     │     └── Builds _nameToGuid, _prefabToGuid, _guidToName, _hashToGuid
        │     │         and _entriesByIndexClass (for alias file generation)
        │     ├── Phase 2: Loads Aliases/*.json — admin name overrides per server
        │     └── GenerateAliasFiles() if GenerateNameAliasConfigs = true
        │
        ├── HeartConfigBuilder.RunIfRequested()
        │     ├── GenerateAllModuleExamples → extract Items/Examples_Item.json
        │     │     from embedded resource, call registered module generators
        │     ├── GenerateHeartExamples     → extract Items/Examples_Item.json only
        │     └── GenerateDebugConfigs      → extract Items/Debug_Item.json,
        │           call registered module debug generators
        │
        ├── ItemService.RegisterDirectory(HeartPathIndex.ItemsDir)
        ├── ItemService.Initialize()
        │     └── Scans all registered dirs recursively for *.json
        │         Parses all fields per entry → LilithItemConfig.AddOverride()
        │         (per-field merge, alphabetical order, later files win)
        │
        ├── LocalizationService.Initialize()   — apply-layer diagnostic only
        ├── InterfaceService.Initialize()       — apply-layer diagnostic only
        │
        ├── LocalizationFileService.Initialize()
        │     └── Scans Localization/<LanguageCode>/ subdirs for *.json
        │         Builds per-language DisplayName/DescriptionText override maps
        │
        ├── Build baseline TierBlobData[] (empty overrides)
        │
        ├── _initialized = true
        │
        ├── Fire OnInitialized event
        │     └── CookbookPlugin.OnHeartInitialized()
        │           ├── CookbookConfigBuilder.GenerateAllRecipesIfRequested()
        │           ├── if GenerateCookbookExamples: GenerateExampleFiles()
        │           ├── if GenerateCookbookDebugConfigs: GenerateDebugFiles()
        │           ├── CookbookLoader.LoadRecipes() / LoadPrisonerFeed()
        │           ├── RecipeSystem.ApplyChanges()
        │           │     └── Heart.RegisterRecipeOverrides()
        │           ├── StationSystem.ApplyChanges()  (two-pass)
        │           │     └── Heart.RegisterStationRecipeChanges()
        │           │         Heart.RegisterPlayerRecipeChanges()
        │           └── ItemFunctionService.ApplyOverrides()
        │                 └── Patches ItemData.MaxAmount on prefab entities (StackSize)
        │
        ├── Rebuild TierBlobData[] with accumulated overrides
        │     └── SyncPayloadCache.Rebuild()
        │           Critical  → ServerIdentity, ServerLanguage, ItemAppearanceOverrides
        │           High      → RecipeOverrides + StationRecipeOverrides
        │           Normal    → PlayerRecipesToAdd/Remove
        │           Low       → reserved (Machinations, Grimoire)
        │           Background→ reserved (Menagerie, Bounty)
        │
        ├── HeartModuleRegistry.LogSummary()
        └── HeartEventBus.Publish(OnWorldReady)
```

---

## Client Connect Sequence

```
Client connects to server
  └── ServerBootstrapSystem.OnUserConnected
        └── ClientConnectPatch.Postfix()
              ├── Resolve userIndex from _NetEndPointToApprovedUserIndex
              ├── Read User + Character entities
              └── Branch on HeartConfig.SyncMode:
                    ChunkPush  → SyncSender.EnqueueSyncTiers()
                                   └── SyncQueue.Enqueue(tierMessages)
                    HttpServer → SyncSender.SendRedirect(httpUrl, fallback)
                                   └── [[LG:sync-url:<url>:<1|0>]]
                    StaticUrl  → SyncSender.SendRedirect(staticUrl, fallback)
                                   └── [[LG:sync-url:<url>:<1|0>]]

Per-frame drain (SchedulerPatch on ServerBootstrapSystem.OnUpdate):
  SyncQueue.HasPending → SyncQueue.Drain()
    └── Creates ≤ChunksPerFrame(10) ChatMessageServerEvent entities per frame

Server-side incoming chat (ServerChatSystemPatch on ServerBootstrapSystem.OnUpdate):
  Queries for ChatMessageServerEvent + FromCharacter entities
  └── [[LG:sync-fallback]]    → SyncSender.EnqueueSyncTiers() for that client
  └── [[LG:lang-request:X]]   → LocalizationSyncSender.HandleRequest()
        └── If language available: enqueue localization payload chunks
        └── If unavailable: send [[LG:lang-unavailable:X]]
```

---

## Soul Initialization Sequence

```
SoulPlugin.Load()
  ├── SoulCoroutineHost.Register()      — IL2CPP MonoBehaviour registration
  ├── SoulLogger.Initialize()
  ├── SoulConfig.Initialize()           — reads LilithsSoul.cfg (DebugLogging, PreferredLanguage)
  └── Harmony.PatchAll()
        │
        ▼  (client world loads)
ClientInitPatch.Postfix()               — hooks GameDataManager.OnUpdate
  └── SyncReceiver.NotifyWorldReady(connectionString)
        ├── LocalizationPatcher.BuildNameMap()
        ├── DescriptionPatcher.BuildMap()
        ├── RecipePatcher.BuildNameMap()
        ├── IconPatcher.BuildSpriteMaps()
        ├── ServerRegistry.Load()
        ├── TryPreApplyCachedSync(connectionString)
        │     └── Read sync.json → ApplyTier() BEFORE CharacterHUD builds
        ├── TryPreApplyCachedLocalization(connectionString)
        │     └── Read localization_<PreferredLanguage>.json → ApplyTier()
        └── If pendingTierPayloads → ApplyTier() for each
```

---

## Payload Application Order (FIXED — DO NOT REORDER)

```
ApplyTier(ServerSyncPayload):
  Critical slice (ItemAppearanceOverrides non-empty):
    1. LocalizationPatcher.ClearPrevious()     — restore prior repointed names
    2. LocalizationPatcher.Apply(payload)      — repoint display names
    3. DescriptionPatcher.Clear()              — restore prior repointed descriptions
    4. DescriptionPatcher.Build(payload)       — repoint descriptions
    5. IconPatcher.ClearPrevious()             — restore original icons
    6. IconPatcher.Apply(payload)              — sprites into ManagedItemData.Icon

  High slice (RecipeOverrides or StationRecipeOverrides non-empty):
    7. RecipePatcher.Apply(...)                — recipe ECS data
    8. RecipePatcher.ApplyStationRecipes(...)  — station buffers

  Normal slice (PlayerRecipesToAdd/Remove non-empty):
    9. RecipePatcher.ApplyPlayerRecipes(...)   — player buffer last
```

> **Names AND descriptions are repointed at the data layer — no UI patch.**
> Both `ManagedItemData.Name` (a `LocalizationKey`) and the `Key` field of
> `ManagedItemData.Description` (a `LocalizedStringBuilderBase`) are
> value-type localization keys. The patchers mint a fresh `AssetGuid` per
> item, write the string into `Localization._LocalizedStrings`, and point the
> key at it. The game's own tooltip pipeline resolves the minted key natively.
>
> **Description repoint requires a struct write-back.** `ManagedItemData.Description`
> is a value-type struct whose getter returns a copy. Setting `.Key` on the
> copy alone is discarded; the whole struct must be assigned back through the
> setter (`var d = item.Description; d.Key = mintedKey; item.Description = d;`).
>
> **LocalizationInjector is retired.** Replaced by LocalizationPatcher and
> DescriptionPatcher which mint fresh keys and never reload the localization table.

---

## Sync Transport Modes

Three delivery modes configured via `HeartConfig.SyncMode`.

### ChunkPush (default)

```
Heart (server)                              Soul (client)
──────────────────────                      ──────────────────
SyncPayloadCache.Rebuild()
  └─ GZip + Base64 per tier
  └─ Split into 440-char chunks
  └─ Compute SHA256 checksum

On client connect:
SyncSender.EnqueueSyncTiers()
  └─ [[LG:begin:T:N:CKSUM]]           ──►  SyncReceiver accumulates
  └─ [[LG:T:NNNN]]<base64chunk>       ──►  per-tier buffer
  └─ [[LG:end:T:CKSUM]]               ──►  verify → decompress → apply → cache
```

### HttpServer

```
Heart: SyncHttpServer starts on HeartConfig.HttpPort (default 7902)
  └─ Serves GET /sync → current full payload JSON

On client connect:
SyncSender.SendRedirect()
  └─ [[LG:sync-url:http://<ip>:<port>/sync:<fallback>]]  ──►  SyncReceiver
        └─ SyncHttpFetcher.Fetch(url)
              ├─ Success → apply + cache
              └─ Failure + fallback=1 → [[LG:sync-fallback]] → Heart enqueues chunks
```

### StaticUrl

```
Admin hosts payload at a URL (CDN, Gist, etc.)
On client connect:
SyncSender.SendRedirect()
  └─ [[LG:sync-url:<StaticSyncUrl>:<fallback>]]  ──►  same fetch path as HttpServer
```

Fallback sentinel flow (HttpServer/StaticUrl failure when SyncFallbackToChunks=true):
```
Soul                                        Heart
────────────────────                        ──────────────────────
[[LG:sync-fallback]] (ChatMessageEvent)  ──►  ServerChatSystemPatch
                                               └─ SyncSender.EnqueueSyncTiers()
```

---

## Multi-Language Localization

```
Server (Heart):
  Localization/<LanguageCode>/    — one subfolder per LanguageCodeEnum value
      *.json                      — DisplayName + DescriptionText overrides only
  LocalizationFileService.Initialize() → loads all configured languages

  DefaultLanguage in HeartConfig → populates ServerSyncPayload.ServerLanguage
  ServerSyncPayload.ItemAppearanceOverrides carries names/descriptions in default language

Client (Soul):
  PreferredLanguage in SoulConfig
  On Critical tier receipt:
    └─ if PreferredLanguage != ServerLanguage:
          [[LG:lang-request:<language>]]  ──►  ServerChatSystemPatch
                └─ LocalizationSyncSender.HandleRequest()
                      ├─ Language available → enqueue localization payload chunks
                      └─ Unavailable → [[LG:lang-unavailable:<language>]]
```

---

## Sync Transport — SyncTier Assignment Guide

| Tier | Value | Use for |
|------|-------|---------|
| Critical | 0 | ItemAppearanceOverrides — must arrive before UI builds |
| High | 1 | RecipeOverrides + StationRecipeOverrides |
| Normal | 2 | PlayerRecipesToAdd/Remove |
| Low | 3 | Quest names/text (Machinations), spell names (Grimoire) |
| Background | 4 | Large datasets — Menagerie breeds, Bounty tables, Conquest unit defs |

---

## Module Registration Pattern

```csharp
// In child module Load():
HeartConfigBuilder.RegisterExampleGenerator(MyConfigBuilder.GenerateExampleFiles);
HeartConfigBuilder.RegisterDebugGenerator(MyConfigBuilder.GenerateDebugFiles);
HeartModuleRegistry.Register(new HeartModuleData { ... });
Heart.OnInitialized += OnHeartInitialized;

// In OnHeartInitialized():
Heart.RegisterRecipeOverrides(overrides);
Heart.RegisterStationRecipeChanges(name, toAdd, toRemove);
```

## Module Contract

A child module must:
1. Reference `LilithsHeart.csproj` via `ProjectReference`
2. Declare `[BepInDependency("audaciousbovine.lilithsheart")]`
3. In `Load()`: init config via `HeartPathIndex.ModuleConfig()`, register generators, register with `HeartModuleRegistry`, subscribe to `Heart.OnInitialized`
4. Check `ModuleEnabled` early — return immediately if false (no ECS work, no registration)
5. In `OnHeartInitialized()`: apply ECS changes, call `Heart.Register*()` methods
6. Fully qualify `MyPluginInfo` as `YourModule.MyPluginInfo` (avoids namespace conflict with Heart)
7. Communicate with other modules exclusively via `HeartEventBus` — no direct cross-module references

---

## Soul UI Architecture — Custom Panels

LilithsSoul builds custom UI panels using Unity's runtime UI API (no Unity Editor, no UXML). All panel GameObjects are constructed programmatically at plugin load time.

### Silent Command Reverse Channel

Soul fires chat messages silently to send player interaction events back to Heart. This is the primary Soul→Heart communication channel.

```
Player interaction in Soul panel
  └─ Creates ChatMessageEvent { MessageText = "[[LG:...]]", MessageType = Local }
        └─ ServerChatSystemPatch intercepts server-side, processes sentinel
        └─ Heart sends response payload back to Soul
        └─ Soul panel refreshes
```

All `[[LG:...]]` sentinels sent from Soul are handled in `ServerChatSystemPatch` — the single home for all Soul→Heart communication.

---

## HeartEventBus — Cross-Module Event Flow

The HeartEventBus is the nervous system connecting all modules. Every significant game event is published here; modules subscribe only to what they need.

**Rule:** Modules communicate exclusively via HeartEventBus. No module holds a direct reference to another module's classes. This preserves independent installability — any module can be absent without breaking others.