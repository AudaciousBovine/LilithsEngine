# Data Flow

## ServerSyncPayload (Primary Data Contract)

The `ServerSyncPayload` class in `LilithsMind/Network/ServerSyncPayload.cs` is the core data contract sent from Heart (server) to Soul (client).

### Structure

```
ServerSyncPayload
├── ServerIdentity: string                              — Sanitized server name (folder key)
├── PayloadHash: string                                 — First 8 hex chars of SHA256 (change detection)
├── ItemAppearanceOverrides: Dictionary<string, ItemAppearanceData>
│     Key: prefab Name or Prefab string
│     Value: { DisplayName?, Tooltip?, Icon? }
│            DisplayName → repointed client-side (LocalizationPatcher)
│            Tooltip     → currently a NO-OP client-side (pending Harmony patch)
│            Icon is self-describing:
│              "vitae.png"              → local PNG in Icons/ folder
│              "Icon_BloodOrb"         → in-game sprite name
│              "https://example.com/x" → URL download + cache
├── RecipeOverrides: Dictionary<string, LilithRecipeData>
│     Key: recipe prefab name
│     Value: { CraftDuration, Requirements, Outputs, ... }
├── StationRecipeOverrides: Dictionary<string, LilithStationData>
│     Key: station prefab name
│     Value: { RecipesToAdd: string[], RecipesToRemove: string[] }
├── PlayerRecipesToAdd: List<string>
└── PlayerRecipesToRemove: List<string>
```

### Admin Config File Format

Files live under `BepInEx/config/LilithsHeart/Items/` (recursive `*.json`).
All fields optional — omit any you don't want to change.

```json
{
  "_readme": "Keys are prefab Name or Prefab string. All fields optional.",
  "Item_BloodEssence_T01": {
    "DisplayName": "Vitae",
    "Tooltip": "Concentrated life force.",
    "Icon": "vitae.png"
  },
  "Item_Weapon_Sword_T01_Bone": {
    "DisplayName": "Bone Cleaver"
  }
}
```

Files load in full-path alphabetical order. Later files win per-field (not per-entry) — one file can set `DisplayName`, another can set `Icon` for the same item.

---

## Build Pipeline (Server Side)

```
Heart.OnInitialize():
  1. LocalizationService.Initialize()
       └── Scans all registered directories recursively for *.json
           Heart registers ItemsDir; modules register their own dirs
           Merges into ItemAppearanceConfig.Overrides (per-field merge)

  2. Build baseline TierBlobData[] (empty overrides)

  3. Fire OnInitialized → modules apply changes + register overrides
       └── CookbookPlugin: RecipeSystem + StationSystem apply changes
           Heart.RegisterRecipeOverrides() / RegisterStationRecipeChanges()

  4. Rebuild TierBlobData[] with accumulated overrides

SyncPayloadCache.Rebuild():
  Per tier: JSON → GZip compress → base64 encode → split into 440-char chunks
  
  Critical  → { ServerIdentity, PayloadHash, ItemAppearanceOverrides }
  High      → { ServerIdentity, PayloadHash, RecipeOverrides, StationRecipeOverrides }
               (only built if non-empty)
  Normal    → { ServerIdentity, PayloadHash, PlayerRecipesToAdd, PlayerRecipesToRemove }
               (only built if non-empty)
  Low       → reserved for future modules (Machinations, Grimoire)
  Background → reserved for large data sets (Menagerie, Bounty)
  
  Each tier: Checksum = SHA256(base64)[..8]
  Cached as TierBlobData[] — immutable until next Rebuild()
```

---

## Transport Protocol (Tiered Chat-Based)

```
No Unity Netcode in IL2CPP → ChatMessageServerEvent with ServerChatMessageType.System

Connect event:
  ClientConnectPatch → SyncSender.EnqueueSyncTiers(userEntity, characterEntity, userIndex)
    └── For each TierBlobData (ordered Critical→Background):
          SyncQueue.Enqueue(messages) where messages =
            [[LG:begin:T:N:CKSUM]]        — begin sentinel
            [[LG:T:0000]]<base64chunk>    — chunk (zero-padded index)
            [[LG:T:0001]]<base64chunk>
            ...
            [[LG:end:T:CKSUM]]            — end sentinel

Per-frame drain (SchedulerPatch on ServerBootstrapSystem.OnUpdate):
  SyncQueue.Drain() — creates at most ChunksPerFrame(10) ECS entities per frame
    └── SyncSender.SendQueuedChunk() creates one ChatMessageServerEvent entity:
          ChatMessageServerEvent { MessageType = System, MessageText = chunk }
          + SendEventToUser { UserIndex = int }  ← routes to correct client

Benefit: connect-frame spike eliminated — cost spread across frames
Typical: 5KB appearance payload → ~12 chunks after GZip+base64 → 2 frames at 10/frame
```

---

## Receive Pipeline (Client Side)

```
ClientChatSystemPatch.Prefix (per-frame, prefix so entities destroyed before UI)
  └── For each ChatMessageServerEvent where MessageType == System:
        SyncReceiver.TryHandleMessage(text)
          ├── [[LG:begin:T:N:CKSUM]] → init tier accumulator, store expected count + checksum
          ├── [[LG:T:NNNN]]<data>   → append chunk to tier accumulator
          ├── [[LG:end:T:CKSUM]]    → ProcessTier()
          │     ├── Verify chunk count + checksum
          │     ├── Concat chunks → base64 decode → GZip decompress → JSON string
          │     ├── Deserialize tier-specific payload
          │     ├── WriteToDiskIfChanged() — SHA256 hash comparison
          │     └── ApplyTier() — applies immediately, no waiting for other tiers
          └── If consumed → DestroyEntity (never shown in chat UI)
```

---

## Payload Application Order (FIXED — DO NOT REORDER)

```
ApplyPayload(ServerSyncPayload):
  1. LocalizationPatcher.ClearPrevious()
       └── Restore each previously repointed item's original Name (LocalizationKey)

  2. LocalizationPatcher.Apply(payload)
       └── For each ItemAppearanceOverrides entry with non-null DisplayName:
             a. Resolve prefab name → PrefabGUID (LilithsMind reflection)
             b. Capture current ManagedItemData.Name for restore
             c. Mint fresh AssetGuid = AssetGuid.FromString(Guid.NewGuid())
             d. Localization._LocalizedStrings[mintedGuid] = DisplayName
             e. ManagedItemData.Name = new LocalizationKey(mintedGuid)
           NO LoadDefaultLanguage — minted keys are never wiped.
           Tooltip field is NOT applied here (pending Harmony patch — no-op).

  3. IconPatcher.ClearPrevious()
       └── Restore original ManagedItemData.Icon for all previously patched items

  4. IconPatcher.Apply(payload)
       └── For each ItemAppearanceOverrides entry with non-null Icon:
             Resolution order:
               a. Local PNG → Icons/ recursive scan, filename match
               b. In-game sprite → Resources.FindObjectsOfTypeAll<Sprite>()
               c. https:// URL → IconDownloader (async, callback on complete)
             → ManagedItemData.Icon = resolvedSprite

  5. RecipePatcher.Apply(payload.RecipeOverrides)
  6. RecipePatcher.ApplyStationRecipes(payload.StationRecipeOverrides)
  7. RecipePatcher.ApplyPlayerRecipes(payload.PlayerRecipesToAdd, ...)
```

### Why repoint instead of overwrite (display names)

Many vanilla items share one localization key by value (e.g. every sword shares
one tooltip key). Overwriting the string at that key changes every item sharing
it. Worse, the retired LocalizationInjector cleared via
`Localization.LoadDefaultLanguage()`, which reloads `_LocalizedStrings` from
disk — so when it ran a second time (cached pre-apply + server payload), it
wiped the keys it had just written and renames reverted to raw GUIDs.
LocalizationPatcher mints a brand-new `AssetGuid` per item (unique, so no
sharing) and points the **value-type** `ManagedItemData.Name` at it, which
persists. It never reloads the table.

`ManagedItemData.Description` is a **reference-type** property whose getter
returns a copy, so assigning it does not persist — tooltips cannot be repointed
through managed data and await a Harmony patch on the tooltip-build path.

---

## Pre-Apply (Cached Sync — UI Race Fix)

```
ClientInitPatch detects world ready
  → SyncReceiver.NotifyWorldReady(connectionString)
    → LocalizationPatcher.BuildNameMap()         — LilithsMind reflection (name→PrefabGUID)
    → RecipePatcher.BuildNameMap()               — PrefabCollectionSystem
    → IconPatcher.BuildSpriteMaps()              — Resources + Icons/ scan
    → ServerRegistry.Load()                      — reads servers.json
    → ServerRegistry.TryGetFolderName(connectionString)
    → Read sync.json from disk
    → Deserialize
    → ApplyPayload()  — BEFORE CharacterHUD builds
    → Later: server payload arrives → ApplyPayload() again (idempotent if hash unchanged)
```

---

## Config File Layout (Server)

```
BepInEx/config/LilithsHeart/
  ├── LilithsHeart.cfg               — DebugLogging, ServerName
  ├── LilithsCookbook.cfg            — GenerateAllRecipes
  ├── Items/                         — *.json item appearance overrides (recursive)
  │     Currencies/
  │     Weapons/
  │     example.json
  ├── Recipes/                       — *.json recipe config (LilithsCookbook)
  ├── Stations/                      — *.json station config (LilithsCookbook)
  ├── MainQuest/                     — *.json quest text (LilithsMachinations, future)
  └── Spells/                        — *.json spell names/tooltips (LilithsGrimoire, future)
```

## Config File Layout (Client)

```
BepInEx/config/LilithsSoul/
  ├── LilithsSoul.cfg                — DebugLogging
  ├── servers.json                   — connection string → folder name mapping
  ├── Icons/                         — PNG icons + URL download cache (recursive)
  │     vitae.png
  │     Weapons/
  │         bone-sword.png
  └── <ServerIdentity>/
        sync.json                    — cached ServerSyncPayload per server
```

---

## ServerEventPayload (In-Session Events)

Reserved — not yet implemented.

```
ServerEventPayload {
    Kind: EventKind  (int, see range reservation)
    Data: string     (JSON-serialized event-specific data)
}

EventKind Range Reservation:
  0-99     Core
  100-199  LilithsCookbook
  200-299  LilithsBounty
  300-399  LilithsTreasury
  400-499  LilithsMachinations
```