# Architecture

## Layer Diagram

```
LilithsMind (pure C#, no game deps)
    ├── Data/ItemAppearanceData.cs       — item appearance DTO
    ├── Prefabs/Definitions/*Index.cs    — static PrefabDef catalog
    ├── Network/*Payload.cs, *Data.cs    — shared DTOs
          │
          ▼
┌──────────────────────────────────────────────┐
│              LilithsHeart (server)             │
│  Foundation/  Events/  Config/  Services/     │
│  Network/     Patches/  Modules/              │
│  Plugin entry: HeartPlugin.cs                  │
│  NuGet: VRising.Unhollowed.Client, VCF         │
└──────────────────────┬───────────────────────┘
                       │ BepInDependency
                       ▼
┌──────────────────────────────────────────────┐
│           LilithsCookbook (server)            │
│  Systems/     Data/     Config/               │
│  Plugin entry: CookbookPlugin.cs              │
│  Depends on Heart + Mind                      │
└──────────────────────────────────────────────┘

LilithsSoul (client, standalone)
    Foundation/    Services/    Config/
    Network/       Patches/
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
        ▼  (world loads — WarEventRegistrySystem fires)
InitializationPatch.Postfix()
  └── Heart.OnInitialize()
        ├── PrefabNameResolver.Initialize()
        │     └── Scans LilithsMind definitions via reflection
        │
        ├── LocalizationService.Initialize()
        │     └── RegisterDirectory(ItemsDir) — Heart registers Items/
        │     └── Modules may register additional dirs before this fires
        │     └── Scans all registered dirs recursively for *.json
        │     └── Merges into ItemAppearanceConfig (per-field, alphabetical)
        │
        ├── Build baseline TierBlobData[] (empty overrides)
        │
        ├── _initialized = true
        │
        ├── Fire OnInitialized event
        │     └── CookbookPlugin.OnHeartInitialized()
        │           ├── CookbookConfigBuilder.GenerateAllRecipesIfRequested()
        │           ├── CookbookLoader.LoadRecipes() / LoadStations()
        │           ├── RecipeSystem.ApplyChanges()
        │           └── StationSystem.ApplyChanges()
        │                 └── Heart.RegisterRecipeOverrides()
        │                 └── Heart.RegisterStationRecipeChanges()
        │                 └── Heart.RegisterPlayerRecipeChanges()
        │
        ├── Rebuild TierBlobData[] with accumulated overrides
        │     └── SyncPayloadCache.Rebuild()
        │           Critical  → ItemAppearanceOverrides (JSON→GZip→base64→chunks)
        │           High      → RecipeOverrides + StationRecipeOverrides
        │           Normal    → PlayerRecipesToAdd/Remove
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
              └── SyncSender.EnqueueSyncTiers(userEntity, characterEntity, userIndex)
                    └── For each TierBlobData (Critical first):
                          SyncQueue.Enqueue(messages)

Per-frame drain (SchedulerPatch on ServerBootstrapSystem.OnUpdate):
  SyncQueue.HasPending → SyncQueue.Drain()
    └── Creates ≤ChunksPerFrame(10) ChatMessageServerEvent entities per frame
    └── Each entity includes SendEventToUser { UserIndex } for routing
```

---

## Soul Initialization Sequence

```
SoulPlugin.Load()
  ├── SoulCoroutineHost.Register()      — IL2CPP MonoBehaviour registration
  ├── SoulLogger.Initialize()
  ├── SoulConfig.Initialize()
  └── Harmony.PatchAll()
        │
        ▼  (client world loads)
ClientInitPatch.Postfix()               — hooks GameDataManager.OnUpdate
  └── SyncReceiver.NotifyWorldReady(connectionString)
        ├── LocalizationPatcher.BuildNameMap()
        │     └── LilithsMind reflection → _nameToPrefabGuid (display-name repoint)
        ├── RecipePatcher.BuildNameMap()
        │     └── PrefabCollectionSystem + LilithsMind → name→GUID
        ├── IconPatcher.BuildSpriteMaps()
        │     ├── LilithsMind reflection → _nameToPrefabGuid
        │     ├── Icons/ recursive scan → _localFiles (filename→path, PNG only)
        │     └── Resources.FindObjectsOfTypeAll<Sprite>() → _gameSprites
        ├── ServerRegistry.Load()           — reads servers.json
        ├── TryPreApplyCachedSync(connectionString)
        │     └── Look up connectionString → folderName
        │     └── Read sync.json from disk
        │     └── ApplyPayload()  — BEFORE CharacterHUD builds
        └── If pendingPayload → ApplyPayload()
```

---

## Payload Application Order (FIXED — DO NOT REORDER)

```
ApplyPayload(ServerSyncPayload):
  1. LocalizationPatcher.ClearPrevious()     — restore prior repointed names
  2. LocalizationPatcher.Apply(payload)      — repoint display names (mint + inject + point Name)
  3. IconPatcher.ClearPrevious()             — restore original icons
  4. IconPatcher.Apply(payload)              — sprites into ManagedItemData.Icon
  5. RecipePatcher.Apply(...)                — recipe ECS data
  6. RecipePatcher.ApplyStationRecipes(...)  — station buffers
  7. RecipePatcher.ApplyPlayerRecipes(...)   — player buffer last
```

> **Display names repointed, not injected.** LocalizationInjector is retired.
> It overwrote shared localization keys and reloaded the table via
> LoadDefaultLanguage() on clear, wiping its own writes on the second apply.
> LocalizationPatcher mints a fresh AssetGuid per item and points the
> value-type ManagedItemData.Name at it — persists, no contamination, no reload.
>
> **Tooltips not yet handled.** ManagedItemData.Description is a reference
> property whose getter returns a copy; a repoint cannot persist through it.
> Tooltip overrides are no-ops pending a Harmony patch (separate task).

---

## LocalizationService Directory Registration Pattern

```
// Heart registers its own directory at init:
LocalizationService.RegisterDirectory(HeartPathIndex.ItemsDir);

// Future modules register theirs in Load() or OnHeartInitialized():
LocalizationService.RegisterDirectory(HeartPathIndex.DataDir("MainQuest"));  // Machinations
LocalizationService.RegisterDirectory(HeartPathIndex.DataDir("Spells"));     // Grimoire

// Each directory scanned recursively — admins organize freely:
Items/
    Currencies/blood-essence.json
    Weapons/swords.json
    items.json
```

---

## Module Registration Pattern

```csharp
// In child module Load():
HeartModuleRegistry.Register(new HeartModuleData
{
    ModuleId   = "audaciousbovine.lilithscookbook",
    ModuleName = "LilithsCookbook",
    Version    = "0.1.0",
});
Heart.OnInitialized += OnHeartInitialized;

// In OnHeartInitialized():
// Apply ECS changes, then register overrides:
Heart.RegisterRecipeOverrides(overrides);
Heart.RegisterStationRecipeChanges(name, toAdd, toRemove);
```

## Module Contract

A child module must:
1. Reference `LilithsHeart.csproj` via `ProjectReference`
2. Declare `[BepInDependency("audaciousbovine.lilithsheart")]`
3. In `Load()`: create config via `HeartPathIndex.ModuleConfig()`, register with `HeartModuleRegistry`, subscribe to `Heart.OnInitialized`
4. In `OnHeartInitialized()`: apply ECS changes, call `Heart.Register*()` methods
5. Fully qualify `MyPluginInfo` as `YourModule.MyPluginInfo` (avoids namespace conflict with Heart)

## SyncTier Assignment Guide

| Tier | Value | Use for |
|------|-------|---------|
| Critical | 0 | ItemAppearanceOverrides — must arrive before UI builds |
| High | 1 | RecipeOverrides + StationRecipeOverrides |
| Normal | 2 | PlayerRecipesToAdd/Remove |
| Low | 3 | Quest names/text (Machinations), spell names (Grimoire) |
| Background | 4 | Large data sets — horse breeding (Menagerie), bounties (Bounty) |