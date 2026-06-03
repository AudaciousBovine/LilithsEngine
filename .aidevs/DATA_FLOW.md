# Data Flow

## ServerSyncPayload (Primary Data Contract)

The `ServerSyncPayload` class in `LilithsMind/Network/ServerSyncPayload.cs` is the core data contract sent from Heart (server) to Soul (client).

### Structure

```
ServerSyncPayload
├── ServerIdentity: string                              — Sanitized server name (folder key)
├── PayloadHash: string                                 — First 8 hex chars of SHA256 (change detection)
├── ItemAppearanceOverrides: Dictionary<string, LilithItemData>
│     Key: prefab Name or Prefab string
│     Value: { DisplayName?, DescriptionText?, Icon? }
│            DisplayName     → repointed client-side (LocalizationPatcher)
│            DescriptionText → repointed client-side (DescriptionPatcher)
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
    "DescriptionText": "Concentrated life force, harvested from the living.",
    "Icon": "vitae.png"
  },
  "Item_Weapon_Sword_T01_Bone": {
    "DisplayName": "Bone Cleaver"
  }
}
```

Files load in full-path alphabetical order. Later files win per-field (not per-entry) — one file can set `DisplayName`, another can set `Icon` for the same item.

> **Field rename:** the appearance field formerly called `Tooltip` is now
> `DescriptionText` (in the `LilithItemData` DTO and the JSON key). There is
> no back-compat shim for the old `Tooltip` key — no live servers existed at the
> time of the rename.

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

  Each tier: Checksum = SHA256(base64 TEXT)[..8], uppercase hex
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
            [[LG:begin:T:N:CKSUM]]        — begin sentinel (T=tier, N=chunk count)
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

> **Encoder note (must match on the receiver):** the WHOLE blob is
> `JSON → GZip → Convert.ToBase64String` (base64'd ONCE), and only THEN sliced
> into 440-char chunks. The checksum is `SHA256` over the **base64 text**
> (uppercase, first 8 hex), not over the gzip bytes. The receiver therefore
> concatenates the chunk strings FIRST, verifies the checksum on that base64
> text, then does a single base64-decode followed by gunzip.

---

## Receive Pipeline (Client Side)

```
ClientChatSystemPatch.Prefix (per-frame, prefix so entities destroyed before UI)
  └── For each ChatMessageServerEvent where MessageType == System:
        SyncReceiver.TryHandleMessage(text)
          ├── [[LG:begin:T:N:CKSUM]] → init tier accumulator, store expected count + checksum
          ├── [[LG:T:NNNN]]<data>   → append chunk string to tier accumulator
          ├── [[LG:end:T:CKSUM]]    → ProcessTier()
          │     ├── Concat chunk strings → SHA256-verify the base64 text
          │     ├── Convert.FromBase64String → GZip decompress → JSON string
          │     ├── Deserialize tier-specific payload
          │     ├── Merge into disk-cache accumulator keyed by PayloadHash
          │     ├── WriteToDiskIfChanged() — SHA256 hash comparison
          │     └── ApplyTier() — applies that tier IMMEDIATELY (no waiting for others)
          └── If consumed → DestroyEntity (never shown in chat UI)
```

Per-tier application (each tier carries only its slice of the payload):

```
Tier Critical (0) → ItemAppearanceOverrides → name, description, icon repoint
Tier High     (1) → Recipe + StationRecipe overrides
Tier Normal   (2) → player recipe add/remove
```

If the client world is not ready when a tier arrives, the deserialized payload
is held in `_pendingTierPayloads` and applied in `NotifyWorldReady()`.

---

## Payload Application Order (FIXED — DO NOT REORDER)

```
ApplyPayload(ServerSyncPayload):   // also the per-tier apply path
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

  3. DescriptionPatcher.Clear()
       └── Restore each previously repointed item's original Description struct
           (item.Description = capturedOriginalStruct)

  4. DescriptionPatcher.Build(payload)
       └── For each ItemAppearanceOverrides entry with non-null DescriptionText:
             a. Resolve prefab name → PrefabGUID (LilithsMind reflection)
             b. Capture current ManagedItemData.Description struct for restore
             c. Mint fresh AssetGuid = AssetGuid.FromString(Guid.NewGuid())
             d. Localization._LocalizedStrings[mintedGuid] = DescriptionText
             e. var d = item.Description;          // STRUCT COPY (value type)
                d.Key = new LocalizationKey(mintedGuid);
                item.Description = d;              // WRITE THE WHOLE STRUCT BACK
           The write-back in (e) is mandatory — see "Why the description
           override is data-layer" below.

  5. IconPatcher.ClearPrevious()
       └── Restore original ManagedItemData.Icon for all previously patched items

  6. IconPatcher.Apply(payload)
       └── For each ItemAppearanceOverrides entry with non-null Icon:
             Resolution order:
               a. Local PNG → Icons/ recursive scan, filename match
               b. In-game sprite → Resources.FindObjectsOfTypeAll<Sprite>()
               c. https:// URL → IconDownloader (async, callback on complete)
             → ManagedItemData.Icon = resolvedSprite

  7. RecipePatcher.Apply(payload.RecipeOverrides)
  8. RecipePatcher.ApplyStationRecipes(payload.StationRecipeOverrides)
  9. RecipePatcher.ApplyPlayerRecipes(payload.PlayerRecipesToAdd, ...)
```

### Why repoint instead of overwrite (names AND descriptions)

Many vanilla items share one localization key by value (e.g. every sword shares
one tooltip key). Overwriting the string at that key changes every item sharing
it. Worse, the retired LocalizationInjector cleared via
`Localization.LoadDefaultLanguage()`, which reloads `_LocalizedStrings` from
disk — so when it ran a second time (cached pre-apply + server payload), it
wiped the keys it had just written and renames reverted to raw GUIDs on screen.

Both `LocalizationPatcher` (names) and `DescriptionPatcher` (descriptions) mint
a brand-new `AssetGuid` per item (unique, so no sharing), write the new string
there, and point the item's value-type localization key at it. Neither reloads
the table. No shared-key contamination.

### Why the description override is data-layer (and not a UI patch)

`ManagedItemData.Description` is a `ProjectM.UI.LocalizedStringBuilderBase`,
which is a **value-type struct** (`[StructLayout(LayoutKind.Explicit)]`) whose
first field is `[FieldOffset(0)] public LocalizationKey Key;`. The tooltip body
resolves from that `Key` via the struct's `Build(EntityManager, Entity)`. So a
description is just a `LocalizationKey` — the same kind of value as `Name`.

The repoint requires writing the WHOLE struct back. The getter returns a *copy*
(value semantics), so mutating `item.Description.Key` in place is discarded.
The fix: read the struct into a local, set `.Key`, assign the local back to
`item.Description`. (An earlier "Description doesn't persist" conclusion was a
false negative caused by mutating the discarded copy.)

A long investigation first tried to override the description by Harmony-patching
the client tooltip-build pipeline. Every attempt failed in this IL2CPP build,
and the conclusion is recorded here so it is not repeated:

| Target | Result |
|--------|--------|
| `SomeReusableSubMenuThings.RefreshGeneralItemTooltip` (Entity, PrefabGUID) | attached, never fired on inventory/hotbar hover |
| `RefreshGeneralItemTooltip` (ItemGridSelectionEntry) | attached, never fired on hover |
| `FakeTooltip.SetData` | crashed client on invocation (inlined/unpatchable) |
| `FakeTooltip.SetTooltip` (public 20-param, has descriptionOverride) | attached, crashed client on hover for ANY item, prefix and postfix alike |

Pattern: the tooltip-build methods that fire on hover crash when patched; the
ones that do not crash never fire. The data-layer repoint sidesteps the UI
entirely — the game resolves the minted key on its own.

---

## Pre-Apply (Cached Sync — UI Race Fix)

```
ClientInitPatch detects world ready
  → SyncReceiver.NotifyWorldReady(connectionString)
    → LocalizationPatcher.BuildNameMap()         — LilithsMind reflection (name→PrefabGUID)
    → DescriptionPatcher.BuildMap()              — LilithsMind reflection (name/prefab→PrefabGUID)
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