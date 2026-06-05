# Data Flow

## ServerSyncPayload (Primary Data Contract)

The `ServerSyncPayload` class in `LilithsMind/Network/ServerSyncPayload.cs` is the core data contract sent from Heart (server) to Soul (client).

### Structure

```
ServerSyncPayload
├── ServerIdentity: string                              — Sanitized server name (folder key)
├── PayloadHash: string                                 — First 8 hex chars of SHA256 (change detection)
├── ServerLanguage: string                              — Language code of ItemAppearanceOverrides content
│                                                         (default "English"; matches LanguageCodeEnum name)
├── ItemAppearanceOverrides: Dictionary<string, LilithItemData>
│     Key: prefab Name alias, Prefab string, or GuidHash integer string
│     Value: { DisplayName?, DescriptionText?, Icon? }
│            — StackSize and ChangesEnabled are filtered out before sending (server-only)
│            DisplayName     → repointed client-side (LocalizationPatcher)
│            DescriptionText → repointed client-side (DescriptionPatcher)
│            Icon is self-describing:
│              "vitae.png"              → local PNG in Icons/ folder (recursive search)
│              "Icon_BloodOrb"         → in-game sprite name
│              "https://example.com/x" → URL download + cache to Icons/ root
├── RecipeOverrides: Dictionary<string, LilithRecipeData>
├── StationRecipeOverrides: Dictionary<string, LilithStationData>
├── PlayerRecipesToAdd: List<string>
└── PlayerRecipesToRemove: List<string>
```

### Admin Config Key Resolution

Config file keys are resolved by `PrefabNameResolver.TryResolve()` in order:
1. **Admin alias / compiled Name** — e.g. `"BloodEssence"`, `"BoneSword"`
2. **Raw prefab string** — e.g. `"Item_BloodEssence_T01"`
3. **Raw GuidHash integer** — e.g. `"-1595790789"` (signed int, useful for unlisted items)

### Admin Config File Format

Files live under `BepInEx/config/LilithsHeart/Items/` (recursive `*.json`).
All fields optional — omit any you don't want to change.

```json
{
  "_readme": "Keys can be Name alias, prefab string, or GuidHash integer.",
  "BloodEssence": {
    "DisplayName": "Vitae",
    "DescriptionText": "Concentrated life force, harvested from the living.",
    "Icon": "vitae.png",
    "ChangesEnabled": true,
    "StackSize": 500
  },
  "Item_Ingredient_Gem_Ruby_T01": {
    "DisplayName": "Bloodstone",
    "Icon": "Icon_BloodOrb"
  },
  "-1595790789": {
    "DisplayName": "Mystery Item"
  }
}
```

Files load in full-path alphabetical order. Later files win per-field (not per-entry) — one file can set `DisplayName`, another sets `StackSize` for the same item and both apply. `ChangesEnabled` gates only functional fields (StackSize); appearance fields always apply when non-null.

---

## Config Generation System

```
HeartConfig flags trigger generation on next world boot:

GenerateHeartExamples:
  └─ Extract Resources/Examples/Examples_Item.json → Items/Examples_Item.json

GenerateAllModuleExamples:
  └─ Extract Resources/Examples/Examples_Item.json → Items/Examples_Item.json
  └─ Call each registered module's GenerateExampleFiles()
       └─ CookbookConfigBuilder.GenerateExampleFiles():
             Extract → Recipes/Examples_Recipe.json
             Extract → Recipes/Examples_PrisonerFeed.json
             Extract → Recipes/Examples_PrisonerFed.json
             Extract → Items/Examples_CookbookItem.json

GenerateDebugConfigs:
  └─ Extract Resources/Debug/Debug_Item.json → Items/Debug_Item.json
  └─ Call each registered module's GenerateDebugFiles()
       └─ CookbookConfigBuilder.GenerateDebugFiles():
             Extract → Recipes/Debug_Recipe.json
             Extract → Recipes/Debug_PrisonerFeed.json
             Extract → Recipes/Debug_PrisonerFed.json
             Extract → Items/Debug_CookbookItem.json

GenerateNameAliasConfigs:
  └─ PrefabNameResolver.GenerateAliasFiles()
       └─ Dumps Aliases/<IndexClassName>.json for each *Index class
          Values are compiled Name defaults — admins edit to set per-server aliases

GenerateCookbookExamples (CookbookConfig):
  └─ CookbookConfigBuilder.GenerateExampleFiles() (same as above, standalone)

GenerateCookbookDebugConfigs (CookbookConfig):
  └─ CookbookConfigBuilder.GenerateDebugFiles() (same as above, standalone)
```

All generation files always overwrite. Flags reset to false after generation.
Example files: `ChangesEnabled=false` — safe to load, no changes applied.
Debug files: `ChangesEnabled=true` — obviously different values for verification.

---

## Build Pipeline (Server Side)

```
Heart.OnInitialize():
  1. PrefabNameResolver.Initialize()
       ├─ Phase 1: Reflects LilithsMind → _nameToGuid, _prefabToGuid,
       │           _guidToName, _hashToGuid, _entriesByIndexClass
       └─ Phase 2: Loads Aliases/*.json → admin name overrides (per-server)

  2. HeartConfigBuilder.RunIfRequested()
       └─ Extracts embedded JSON resources if generation flags set

  3. ItemService.Initialize()
       └─ Scans Items/ recursively for *.json
           Parses DisplayName, DescriptionText, Icon, ChangesEnabled, StackSize
           → LilithItemConfig.AddOverride() (per-field merge, alpha order)

  4. LocalizationService.Initialize()    — diagnostic only (logs entry counts)
     InterfaceService.Initialize()        — diagnostic only (logs entry counts)

  5. LocalizationFileService.Initialize()
       └─ Scans Localization/<LanguageCode>/ subdirs
           Builds per-language {name → {DisplayName?, DescriptionText?}} maps

  6. Build baseline TierBlobData[] (empty overrides)

  7. Fire OnInitialized → CookbookPlugin:
       ├─ CookbookLoader.LoadRecipes() / LoadPrisonerFeed()
       ├─ RecipeSystem.ApplyChanges()      → Heart.RegisterRecipeOverrides()
       ├─ StationSystem.ApplyChanges()     → Heart.RegisterStationRecipeChanges()
       │     (two-pass: prefab entities first, then live entities after RegisterGameData())
       └─ ItemFunctionService.ApplyOverrides()
             └─ Patches ItemData.MaxAmount for all ChangesEnabled=true StackSize entries

  8. Rebuild TierBlobData[] with accumulated overrides

SyncPayloadCache.Rebuild():
  ├─ Filter appearance payload: only entries with non-null DisplayName/DescriptionText/Icon
  │  (StackSize and ChangesEnabled excluded — server-only fields)
  ├─ Populate ServerLanguage from HeartConfig.DefaultLanguage
  │
  ├─ Per tier: JSON → GZip compress → base64 encode → split into 440-char chunks
  │
  │  Critical  → { ServerIdentity, ServerLanguage, PayloadHash, ItemAppearanceOverrides }
  │  High      → { ServerIdentity, PayloadHash, RecipeOverrides, StationRecipeOverrides }
  │               (only built if non-empty)
  │  Normal    → { ServerIdentity, PayloadHash, PlayerRecipesToAdd, PlayerRecipesToRemove }
  │               (only built if non-empty)
  │  Low       → reserved (Machinations, Grimoire)
  │  Background→ reserved (Menagerie, Bounty)
  │
  └─ If SyncMode == HttpServer: SyncHttpServer.UpdatePayload(fullPayload)
```

---

## Transport Protocol

### ChunkPush (default)

```
Connect event:
  ClientConnectPatch → SyncSender.EnqueueSyncTiers(userEntity, characterEntity, userIndex)
    └── For each TierBlobData (ordered Critical→Background):
          SyncQueue.Enqueue(messages) where messages =
            [[LG:begin:T:N:CKSUM]]        — begin sentinel (T=tier, N=chunk count)
            [[LG:T:0000]]<base64chunk>    — chunk (zero-padded index)
            [[LG:T:0001]]<base64chunk>
            ...
            [[LG:end:T:CKSUM]]            — end sentinel

Per-frame drain (SchedulerPatch on ServerBootstrapSystem.OnUpdate):
  SyncQueue.Drain() — creates at most ChunksPerFrame(10) ECS entities per frame
```

> **Encoder note:** the WHOLE blob is `JSON → GZip → Convert.ToBase64String`
> (base64'd ONCE), then sliced into 440-char chunks. Checksum = SHA256 over the
> base64 TEXT (uppercase, first 8 hex). Receiver concatenates chunks FIRST,
> verifies checksum on base64 text, then base64-decode → gunzip.

### HttpServer

```
Heart startup: SyncHttpServer.Start() on HeartConfig.HttpPort (default 7902)
  └─ Background thread HttpListener serves GET /sync → payload JSON

Connect: SyncSender.SendRedirect(url, fallback)
  └─ [[LG:sync-url:http://<ip>:<port>/sync:<1|0>]]

Soul receipt: SyncReceiver.HandleRedirect(message)
  ├─ Parse URL + fallback flag (split from END — URL may contain colons)
  └─ SyncHttpFetcher.Fetch(url, onSuccess, onFailure)
        ├─ 10s timeout UnityWebRequest
        ├─ Success → apply + cache
        └─ Failure + fallback=1 → SendFallbackSentinel()
              └─ ChatMessageEvent { MessageType = Local } in client ECS world
                    └─ ServerChatSystemPatch intercepts [[LG:sync-fallback]]
                          └─ SyncSender.EnqueueSyncTiers() for that client
```

### StaticUrl

Identical to HttpServer Soul-side fetch path. URL comes from `HeartConfig.StaticSyncUrl`. Heart hosts nothing.

---

## Receive Pipeline (Client Side)

```
ClientChatSystemPatch.Prefix (per-frame):
  └── SyncReceiver.TryHandleMessage(text)
        ├── [[LG:sync-url:<url>:<fallback>]]   → HandleRedirect()
        │     └─ SyncHttpFetcher or SendFallbackSentinel
        ├── [[LG:lang-unavailable:<lang>]]      → log warning, stay on default
        ├── [[LG:begin:T:N:CKSUM]]              → init tier accumulator
        ├── [[LG:T:NNNN]]<data>                 → append chunk to accumulator
        └── [[LG:end:T:CKSUM]]                  → HandleEnd()
              ├─ Concat chunks → SHA256-verify base64 text
              ├─ Convert.FromBase64String → GZip decompress → JSON
              ├─ Deserialize tier-specific payload
              ├─ Check ServerLanguage vs PreferredLanguage (Critical tier only)
              │    └─ If different → SendChatMessage([[LG:lang-request:<lang>]])
              ├─ if localization payload → WriteLocalizationToDisk()
              │    else → MergeAndCache() → WriteToDisk()
              └─ ApplyTier() (or queue if world not ready)
```

---

## Payload Application Order (FIXED — DO NOT REORDER)

```
ApplyTier(ServerSyncPayload):

  Critical slice (ItemAppearanceOverrides non-empty):
    1. LocalizationPatcher.ClearPrevious()
    2. LocalizationPatcher.Apply(payload)
         └─ For each DisplayName: mint AssetGuid → inject string →
            ManagedItemData.Name = new LocalizationKey(guid)
    3. DescriptionPatcher.Clear()
    4. DescriptionPatcher.Build(payload)
         └─ For each DescriptionText: mint AssetGuid → inject string →
            var d = item.Description; d.Key = new LocalizationKey(guid);
            item.Description = d;  ← MANDATORY struct write-back
    5. IconPatcher.ClearPrevious()
    6. IconPatcher.Apply(payload)
         └─ Resolution: (1) https:// → IconDownloader
                        (2) local PNG → _localFiles recursive lookup
                        (3) in-game sprite → _gameSprites

  High slice (RecipeOverrides or StationRecipeOverrides non-empty):
    7. RecipePatcher.Apply(...)
    8. RecipePatcher.ApplyStationRecipes(...)

  Normal slice (PlayerRecipesToAdd/Remove non-empty):
    9. RecipePatcher.ApplyPlayerRecipes(...)
```

### Why repoint instead of overwrite

Many vanilla items share one localization key by value. Overwriting the string at that key changes every item sharing it. Both `LocalizationPatcher` and `DescriptionPatcher` mint a fresh `AssetGuid` per item — unique, so no sharing. Neither reloads the localization table.

### Why description override is data-layer

`ManagedItemData.Description` is a value-type struct (`LocalizedStringBuilderBase`) — its getter returns a copy. Patching tooltip-build UI methods was attempted and failed in this IL2CPP build (every target either crashed or never fired on hover). The data-layer repoint sidesteps the UI entirely.

---

## Multi-Language Localization Flow

```
Server (Heart):
  Localization/
      Spanish/   *.json  — { "BloodEssence": { "DisplayName": "Vitae (ES)", ... } }
      French/    *.json

  LocalizationFileService.Initialize() → loads per-language maps
  HeartConfig.DefaultLanguage → populates ServerSyncPayload.ServerLanguage

Client (Soul):
  SoulConfig.PreferredLanguage = Spanish

  On Critical tier receipt:
    ServerLanguage="English", PreferredLanguage="Spanish"
    └─ SyncReceiver sends [[LG:lang-request:Spanish]]
          └─ ServerChatSystemPatch → LocalizationSyncSender.HandleRequest()
                ├─ LocalizationFileService.HasLanguage("Spanish") = true
                ├─ BuildLocalizationPayload() → ServerSyncPayload (DisplayName+DescriptionText only)
                └─ EnqueueLocalizationPayload() → chunks via SyncQueue

  Soul receives localization payload:
    └─ WriteLocalizationToDisk() → LilithsGarden/localization_Spanish.json
    └─ ApplyTier() → overwrites DisplayName/DescriptionText from Spanish overrides

  On reconnect:
    └─ TryPreApplyCachedLocalization() reads localization_Spanish.json
    └─ ApplyTier() before UI builds
```

---

## Pre-Apply (Cached Sync — UI Race Fix)

```
ClientInitPatch detects world ready
  → SyncReceiver.NotifyWorldReady(connectionString)
    → Build all patcher lookup tables
    → TryPreApplyCachedSync(connectionString)
          └─ Read sync.json → ApplyTier() BEFORE CharacterHUD builds
    → TryPreApplyCachedLocalization(connectionString)
          └─ Read localization_<PreferredLanguage>.json → ApplyTier()
    → Apply any pending tier payloads that arrived before world was ready
```

---

## Config File Layout (Server)

```
BepInEx/config/LilithsHeart/
  ├── LilithsHeart.cfg               — ServerName, ChunksPerFrame, DefaultLanguage,
  │                                    SyncMode, HttpPort, StaticSyncUrl,
  │                                    SyncFallbackToChunks, DebugLogging,
  │                                    GenerateHeartExamples, GenerateAllModuleExamples,
  │                                    GenerateDebugConfigs, GenerateNameAliasConfigs
  ├── LilithsCookbook.cfg            — ModuleEnabled, GenerateAllRecipes,
  │                                    GenerateCookbookExamples, GenerateCookbookDebugConfigs
  ├── Aliases/                       — per-server prefab name alias overrides
  │     WeaponsIndex.json            — "Item_Weapon_Sword_T01_Bone": "BoneSword"
  │     StationsIndex.json
  │     ...
  ├── Items/                         — *.json item overrides (recursive)
  │     Examples_Item.json           — generated on demand
  │     Debug_Item.json              — generated on demand
  │     Examples_CookbookItem.json   — generated on demand
  │     Debug_CookbookItem.json      — generated on demand
  │     my-items.json                — admin-authored
  ├── Recipes/                       — *.json recipe config (LilithsCookbook)
  │     Examples_Recipe.json
  │     Debug_Recipe.json
  │     Examples_PrisonerFeed.json
  │     Debug_PrisonerFeed.json
  │     Examples_PrisonerFed.json
  │     Debug_PrisonerFed.json
  │     AllRecipes.json              — generated on demand (ECS dump)
  └── Localization/                  — per-language item name/description overrides
        Spanish/
            items-es.json
        French/
            items-fr.json
```

## Config File Layout (Client)

```
BepInEx/config/LilithsSoul/
  ├── LilithsSoul.cfg                — DebugLogging, PreferredLanguage
  ├── servers.json                   — connection string → server identity mapping
  ├── Icons/                         — PNG icons + URL download cache (recursive)
  │     vitae.png
  │     Weapons/
  │         bone-sword.png
  └── <ServerIdentity>/
        sync.json                    — cached ServerSyncPayload (all tiers merged)
        localization_Spanish.json    — cached localization payload for Spanish
        localization_French.json     — cached localization payload for French
```

---

## ServerEventPayload (In-Session Events)

Reserved — not yet implemented.

```
ServerEventPayload {
    Kind: EventKind  (int, see range reservation)
    Data: string     (JSON-serialized event-specific data)
}
```