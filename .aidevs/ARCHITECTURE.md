# Architecture

## Layer Diagram

```
LilithsMind (pure C#, no game deps)
    ├── Data/LilithItemData.cs       — item appearance DTO
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
        │           Low       → Quest/spell names (Machinations, Grimoire)
        │           Background→ Large datasets (Menagerie breeds, Bounty tables)
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
        │     └── LilithsMind reflection → _nameToPrefabGuid
        ├── DescriptionPatcher.BuildMap()
        │     └── LilithsMind reflection → name/prefab → PrefabGUID
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
  2. LocalizationPatcher.Apply(payload)      — repoint display names
  3. DescriptionPatcher.Clear()              — restore prior repointed descriptions
  4. DescriptionPatcher.Build(payload)       — repoint descriptions
  5. IconPatcher.ClearPrevious()             — restore original icons
  6. IconPatcher.Apply(payload)              — sprites into ManagedItemData.Icon
  7. RecipePatcher.Apply(...)                — recipe ECS data
  8. RecipePatcher.ApplyStationRecipes(...)  — station buffers
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
> is a value-type struct whose getter returns a *copy*. Setting `.Key` on the
> copy alone is discarded; the whole struct must be assigned back through the
> setter (`var d = item.Description; d.Key = mintedKey; item.Description = d;`).
>
> **LocalizationInjector is retired.** Replaced by LocalizationPatcher and
> DescriptionPatcher which mint fresh keys and never reload the localization table.

---

## Sync Transport — Chunk Protocol (Current)

The primary sync delivery mechanism uses V Rising's chat message system as a transport channel.

```
Heart (server)                              Soul (client)
──────────────────────                      ──────────────────
SyncPayloadCache.Rebuild()
  └─ GZip + Base64 per tier
  └─ Split into 450-char chunks
  └─ Compute SHA256 checksum

On client connect:
SyncSender.EnqueueSyncTiers()
  └─ [[LG:begin:T:N:CKSUM]]           ──►  SyncReceiver accumulates
  └─ [[LG:T:NNNN]]<base64chunk>       ──►  per-tier buffer
  └─ [[LG:end:T:CKSUM]]               ──►  verify checksum
                                           decompress
                                           deserialize
                                           ApplyPayload()
                                           cache to disk
```

Tiers are sent in fixed order: Critical → High → Normal → Low → Background.
Each tier is applied immediately on receipt before the next tier begins.

---

## Sync Transport — HTTP (Planned)

An alternative sync delivery path that eliminates per-client chunk sending overhead.
Planned for future implementation. The chunk protocol remains as fallback.

```
Heart startup:
  SyncHttpServer starts on configured port (default 7902)
  └─ Serves GET /sync.json → current full payload blob
  └─ Serves GET /stash/{steamId}.json → per-player stash (future)

Soul at world ready:
  SyncHttpFetcher attempts: http://{serverIp}:{port}/sync.json
  └─ Success → deserialize, ApplyPayload(), cache to disk
  └─ Fail    → fall back to disk cache
  └─ Fail    → fall back to chunk transport (wait for [[LG:begin]])
```

**Advantages over chunk transport:**
- O(1) server work regardless of how many clients connect simultaneously
- No payload size ceiling — chunk transport has practical message frequency limits
- Faster client connect experience — single HTTP fetch vs dozens of chat frames
- Per-player stash delivery becomes a simple targeted GET rather than a targeted push

**Security note:** The HTTP endpoint serves read-only mod configuration data. No
player credentials or sensitive data are served. Server admins must open the
configured port in their firewall. Disabled by default; opt-in via Heart config.

---

## Soul UI Architecture — Custom Panels

LilithsSoul builds custom UI panels using Unity's runtime UI API (no Unity Editor,
no UXML). All panel GameObjects are constructed programmatically at plugin load time.

### Panel Construction Pattern

```csharp
// All panels follow this structure:
GameObject panel = new GameObject("LilithsPanel_<Name>");
DontDestroyOnLoad(panel);
panel.AddComponent<Canvas>().sortingOrder = <above HUD>;
panel.AddComponent<CanvasGroup>();   // for fade/disable
// ... children: Background Image, TitleBar, content area
```

Canvas sort order must be set above the game's HUD canvas to avoid clipping.
All panel MonoBehaviour subclasses must be registered via
`ClassInjector.RegisterTypeInIl2Cpp<T>()` in `SoulPlugin.Load()`.

### Proximity Trigger System (Planned)

Allows custom furniture entities to open Soul panels when the player approaches,
mimicking the feel of vanilla crafting station interaction without modifying the
furniture entity's ECS archetype.

```
Heart config defines custom stations:
  { PrefabGUID, StationKey, InteractRange, PanelType }

Heart syncs station world positions + keys to Soul at connect time

Soul ProximityMonitor (per-frame, lightweight):
  └─ Distance-squared check against known station positions
        └─ Player within range + presses interact key
              └─ Opens panel identified by StationKey
              └─ Fires silent VCF command to notify Heart
              └─ Soul renders "Press F — <StationName>" prompt

[PERFORMANCE] Distance-squared comparisons only — no sqrt.
              Small fixed list of known stations — O(n) where n is tiny.
              No ECS queries per frame.
```

**Reusable for all custom panels:**
- Treasury Stash (stash management panel)
- Custom crafting table (custom recipe crafting panel)
- Menagerie stations (creature management panels)
- Conquest table (expedition and unit management panel)
- Notice board (quest journal panel — LilithsMachinations)
- Nexus stone (teleport panel — LilithsNexus)
- Ritual altar (ritual management panel — LilithsBlessings)

### Silent Command Reverse Channel

Soul fires VCF commands silently (without chat echo) to send player interaction
events back to Heart. This is the primary Soul→Heart communication channel for
all panel interactions.

```
Player interaction in Soul panel
  └─ StashCommandDispatcher.Fire(".stash take Oak 10")
        └─ Creates ChatMessageClientEvent in client ECS world
        └─ VCF intercepts server-side, processes command
        └─ Heart sends updated stash payload back to Soul
        └─ Soul panel refreshes
```

---

## Stash Architecture (Planned — LilithsTreasury)

Two distinct stash stores per player, both persisted to JSON on disk.

```
PlayerStash                          CastleStash
────────────────────                 ────────────────────
Bound to: player character           Bound to: castle heart entity
Access: anywhere                     Access: proximity to own castle only
Death (PvP): transferred to killer   Death: unaffected
Death (PvE): configurable            Death: unaffected
  - Lost entirely                    Clan sharing: configurable
  - Moved to CastleStash
  - Configurable per item category
```

**Stash item categories:**
- **Semantic variants** — subsets of vanilla items (Oak, Birch as wood variants).
  Have a BackingItem and ConvertRatio. Players convert real inventory items in/out.
- **Pure currencies** — no backing item. Granted directly by server events
  (bounty rewards, quest completion, ritual grants).
- **Magic aspects** — specialised currencies for spell/ritual gating.

**Convert/Redeem flow:**
```
.stash convert Oak 100
  └─ Heart verifies player has 100 Item_Resource_Wood in inventory
  └─ Heart credits 100 Oak to PlayerStash  ← write-ahead (credit first)
  └─ Heart deducts 100 Wood from ECS inventory
  └─ Heart sends updated StashPayload to Soul

.stash redeem Oak 50
  └─ Heart verifies PlayerStash has 50 Oak
  └─ Heart debits 50 Oak from PlayerStash
  └─ Heart spawns 50 Item_Resource_Wood into player ECS inventory
  └─ Heart sends updated StashPayload to Soul
```

---

## HeartEventBus — Cross-Module Event Flow

The HeartEventBus is the nervous system connecting all modules. Every significant
game event is published here; modules subscribe only to what they need.

```
Event sources (publishers):              Event consumers (subscribers):
─────────────────────────────            ──────────────────────────────
Adversaries → kill events                Machinations  — quest objectives
             infamy threshold events     Bounty        — drop overrides
             faction response events     Wisdom        — unlock conditions
                                         Treasury      — currency grants
Cookbook    → craft events               Blessings     — ritual progress
Bounty      → drop/harvest events        Conquest      — expedition events
Treasury    → stash change events
Menagerie   → capture/breed events       All modules can subscribe to
Conquest    → expedition events          HeartEventBus events from any
Blessings   → ritual complete events     other module — no direct deps.
Nexus       → teleport events
Wisdom      → unlock events
```

**Rule:** Modules communicate exclusively via HeartEventBus. No module holds a
direct reference to another module's classes. This preserves independent
installability — any module can be absent without breaking others.

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
6. Communicate with other modules exclusively via `HeartEventBus` — no direct cross-module references

## SyncTier Assignment Guide

| Tier | Value | Use for |
|------|-------|---------|
| Critical | 0 | ItemAppearanceOverrides — must arrive before UI builds |
| High | 1 | RecipeOverrides + StationRecipeOverrides |
| Normal | 2 | PlayerRecipesToAdd/Remove |
| Low | 3 | Quest names/text (Machinations), spell names (Grimoire), stash item definitions |
| Background | 4 | Large datasets — Menagerie breed definitions, Bounty drop tables, Conquest unit defs |