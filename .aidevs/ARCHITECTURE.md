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
    Network/       Patches/     LUI/
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

---

## LUI Framework (LilithUserInterface)

LUI is LilithsSoul's data-driven UI framework. It provides a complete panel system built on Unity's legacy `UnityEngine.UI` runtime API — no UXML, no Unity Editor prefabs, no TextMeshPro assets. All GameObjects are constructed programmatically. Visual fidelity comes from V Rising's own extracted textures, loaded from loose PNG files and registered in a central `LUIAssets` registry.

The framework has four layers:

- **Asset pipeline** — loads extracted V Rising textures, constructs nine-sliced sprites, registers named assets
- **Element vocabulary** — typed UI element definitions composed into panel layouts
- **JSON layout system** — data-driven panel definitions; modules ship their own `*.layout.json` files
- **Permission layer** — server-defined tiers gate which panels a client can open or interact with

LUI is gated behind `SoulConfig.LUIEnabled` (default `true`). The config editor subsystem within LUI is separately gated behind `SoulConfig.ConfigEditorEnabled` (default `false`). When `ConfigEditorEnabled` is false, no admin UI machinery runs — no AdminSyncPayload handling, no directory navigation, no file caching.

---

### LUI — Asset Pipeline

**Source:** PNG files extracted from V Rising ship in a `LUI/` subdirectory alongside the Soul plugin. `SoulPathIndex.LUIAssetsDir` points to this folder.

**Loading:** `LUIAssetLoader` scans `LUIAssetsDir` at world-ready time, loads each PNG as a `Texture2D`, constructs a `Sprite` via `Sprite.Create()`, and registers it in `LUIAssets`.

**Nine-slicing:** `Sprite.Create(texture, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect, border)` where `border` is `Vector4(left, bottom, right, top)` in pixels. Border values are constants defined once in `LUIAssets` per texture and applied globally. Hand-edited PNG borders are supported — measurements are set as pixel constants in code.

**Registry:** `LUIAssets` is a static class exposing named `Sprite` properties. No panel or component ever does its own file I/O or texture loading.

```
LUIAssets.WindowBackground
LUIAssets.ButtonSmallNormal / ButtonSmallHover / ButtonSmallPressed
LUIAssets.ButtonMediumNormal / ButtonMediumHover / ButtonMediumPressed
LUIAssets.TabActive / TabInactive
LUIAssets.PanelBorder
LUIAssets.GradientHeader
LUIAssets.Separator
// etc. — one entry per extracted texture variant
```

**Layout discovery:** Soul scans `SoulPathIndex.LUILayoutsDir` recursively for `*.layout.json` files at world-ready time. Each module places its own layout JSON in this directory. Panel definitions are additive — installing a module automatically adds its panels with no Soul-side code changes required.

**IL2CPP constraint:** Generic `AddComponent<T>()` is unreliable in IL2CPP. All component attachment uses `go.AddComponent(Il2CppType.Of<T>()).Cast<T>()`. Established pattern from `SoulCoroutineHost`.

---

### LUI — Element Vocabulary

Every element has a mandatory `"Name"` string that must be unique within its parent scope. Elements reference other elements and panels by name. The `Name` field is the connective tissue of the entire layout system.

Elements are either **containers** (hold other elements) or **leaves** (display or interact).

**Containers:**

| Type | Description |
|------|-------------|
| `LilithPanel` | Top-level draggable window. 9-sliced background texture, title bar, close button. Configurable dimensions. |
| `LilithButtonTray` | Grid container. Configurable cell size (width × height), row/column count, growth direction (horizontal/vertical). |
| `LilithTabBar` | Horizontal or vertical tab strip. Each tab maps to a named content pane via `"Content"` field. |
| `LilithScrollView` | Scrollable content area with optional auto-generated scrollbar. |
| `LilithGroup` | Invisible layout container. Groups elements without a visual wrapper. |

**Leaves:**

| Type | Description |
|------|-------------|
| `LilithButton` | Texture set (normal/hover/pressed), text label, action definition. Standalone or tray member. |
| `LilithToggle` | Boolean on/off. Separate textures per state. Binds to a config key or fires an action. |
| `LilithDropdown` | Opens a selection list. Selection fires an action or writes a config value. |
| `LilithTextBox` | Editable text input. Optional validation: `"Numeric"`, `"MaxLength"`. |
| `LilithLabel` | Static or data-bound text display. |
| `LilithImage` | Static sprite display. Optional hover tooltip. |
| `LilithSlider` | Horizontal or vertical value range. Optional `"Step"` snapping. |
| `LilithSeparator` | Decorative divider line or ornamental element. |

**Button action vocabulary:**

```json
{ "Action": "OpenPanel",   "Target": "PanelName" }
{ "Action": "ClosePanel" }
{ "Action": "TogglePanel", "Target": "PanelName" }
{ "Action": "ChatCommand", "Target": "[[LG:command:args]]" }
{ "Action": "SetConfig",   "Target": "ConfigKey", "Value": "..." }
{ "Action": "TriggerHud",  "Target": "HudElementName" }
```

---

### LUI — JSON Layout System

Panels are defined in `*.layout.json` files. Soul discovers all layout files at startup and loads them additively. No hardcoded panel definitions exist in Soul code — all structure comes from JSON.

**Definition model:** A global `"Components"` registry within a layout file holds reusable element definitions referenced by name. Elements may also be defined inline as children of their parent. The hybrid allows reuse where needed without forcing a flat structure everywhere.

**Example panel skeleton:**

```json
{
  "Name": "ExamplePanel",
  "Type": "LilithPanel",
  "Title": "Example",
  "Width": 600,
  "Height": 450,
  "Background": "WindowBackground",
  "Permission": "Player",
  "Children": [
    {
      "Name": "MainTabs",
      "Type": "LilithTabBar",
      "Tabs": [
        { "Name": "OverviewTab", "Label": "Overview", "Content": "OverviewPane" },
        { "Name": "SettingsTab", "Label": "Settings", "Content": "SettingsPane" }
      ]
    }
  ]
}
```

**Module layout delivery:** Each module places a `*.layout.json` alongside its BepInEx plugin DLL. Soul's layout discovery picks it up automatically. No registration call is required.

---

### LUI — Permission Layer

Permission tiers are defined server-side in `lilithpermissions.json` and travel to Soul as part of the standard `ServerSyncPayload` (Critical tier). The data is small and needed immediately. Soul knows its own tier on connect and shows or hides panels accordingly.

**UI gating is cosmetic.** Heart verifies the permission tier of every incoming `[[LG:...]]` sentinel server-side regardless of what the client displays. A player who bypassed UI gating would have their commands silently rejected.

**Tiers (lowest to highest privilege):**

| Tier | Access |
|------|--------|
| `Player` | Default. Player information and interaction panels. |
| `Moderator` | Trusted player. Moderation panels. |
| `Admin` | Elevated. Config editor, admin information and action panels. |
| `Owner` | Has server file access. Defines all other tiers in `lilithpermissions.json`. |

**Server-side `lilithpermissions.json`:**

```json
{
  "Tiers": {
    "Admin":     ["steamid1", "steamid2"],
    "Moderator": ["steamid3"]
  },
  "PanelPermissions": {
    "ServerConfigPanel": "Admin",
    "PlayerStashPanel":  "Player",
    "ModerationPanel":   "Moderator"
  }
}
```

**Mid-session permission changes:** Panels already open remain open but go inert. Any sentinel fired from an inert panel is rejected server-side. No forced close occurs — the UI is simply non-functional until the session ends.

---

### LUI — Floating HUD Button

A persistent draggable button rendered on a high `sortingOrder` Canvas. Implemented as a `MonoBehaviour` registered via `ClassInjector` implementing `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler` from `UnityEngine.EventSystems`. On drag end, position is clamped to screen bounds and persisted to `SoulConfig`. Clicking toggles the main LUI panel.

Rendered using extracted V Rising button textures from `LUIAssets`. Zero per-frame cost at idle — entirely event-driven.

---

### LUI — AdminSyncPayload and Config Editor

The config editor is gated behind `SoulConfig.ConfigEditorEnabled` (default `false`). When disabled, none of the admin UI machinery initializes.

**AdminSyncPayload** is a separate heavyweight payload. It is never pushed to regular players and is never part of the standard connect flow. An admin explicitly requests it by opening the config editor panel, which fires the initial directory ping.

**Lazy directory navigation — three-ping model:**

```
Ping 1: [[LG:admin:dir:root]]
  Heart → top-level folder list with per-entry timestamps

Ping 2: [[LG:admin:dir:Recipes/Stations]]
  Heart → subfolder and file list with per-file Modified timestamps

Ping 3: [[LG:admin:file:Recipes/Stations/alchemy.json]]
  Heart → file content JSON + schema JSON for field rendering
```

**Directory listing response format:**

```json
{
  "Path": "Recipes/Stations",
  "Entries": [
    { "Name": "alchemy.json", "Type": "File",   "Modified": "2026-06-06T14:23:11Z" },
    { "Name": "forge.json",   "Type": "File",   "Modified": "2026-06-05T09:47:03Z" },
    { "Name": "Equipment",    "Type": "Folder" }
  ]
}
```

**Client-side caching:** Individual file payloads are cached in memory with their `Modified` timestamp. Directory listings are always fetched fresh (cheap — names and timestamps only). On re-navigation to a previously loaded file, Soul compares the cached `Modified` timestamp against the directory listing — match opens from cache instantly, mismatch triggers a re-fetch with a loading indicator. The AdminSyncPayload cache is not persisted to disk — always fetched fresh on reconnect.

**Delivery:** AdminSyncPayload uses the same tiered chunk delivery system as standard sync. Server owners should inform players that lag may occur during active admin config sessions.

**Staleness:** Last write wins. Simultaneous editing of the same file by two admins is a communication problem, not a software problem. No conflict resolution is implemented.

**Staged edits:** Changes are held locally in the panel until the admin explicitly saves. Save transmits only the changed fields (delta) via `[[LG:admin:save:<path>]]`. Heart validates permission, writes the file, and updates the `Modified` timestamp. Changes never take effect immediately — a server reload or designated reload command is required to apply them to the live server.

**Schema-driven rendering:** Heart includes the config schema alongside file content in the Ping 3 response. The schema describes each field's type, constraints, display name, description, and minimum permission tier required to edit it. The editor panel builds its field list entirely from the schema — adding a new module automatically adds its config section to the editor with zero Soul-side changes.

---

### LUI — HUD Replacement System

LUI provides an optional HUD layer that can supplement or replace elements of the vanilla V Rising HUD. All LUI HUD elements are rendered on a dedicated high-order Canvas separate from the panel Canvas.

**Lock/unlock:** A HUD settings panel toggles edit mode. When unlocked, every HUD element shows a drag handle and a visibility toggle. When locked, handles disappear and elements are fixed in place. Position and visibility state are persisted to `SoulConfig` per element.

**Vanilla HUD approach — overlay:** LUI HUD elements render above the vanilla HUD. Vanilla elements can be individually hidden via `SetActive(false)` once their GameObject paths are confirmed stable, offered as an opt-in setting per element. This avoids fragility from Stunlock restructuring their UI hierarchy across game updates. Full replace mode is not implemented — overlay is the default and recommended approach.

**Supported elements:**

| Element | Notes |
|---------|-------|
| Health orb / bar | — |
| Blood meter and type indicator | — |
| Buff / debuff icon strip | — |
| Character portrait | — |
| Ability bar | Configurable slot count expansion |
| Spell bar | Configurable slot count expansion |
| Item slots / consumables | Configurable slot count expansion |
| Custom resources | Module-registered (see below) |

**Custom resource registration:** Modules register additional HUD resource bars at startup via `SoulHudRegistry.RegisterResource(key, label, colour)`. Heart includes current values for registered resources in sync payloads or targeted updates. Examples: ritual progress (LilithsBlessings), conquest points (LilithsConquest), infamy per faction (LilithsAdversaries). Bars only appear when the registering module is active.