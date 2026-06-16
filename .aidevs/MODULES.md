# Modules — Planned Module Design Reference

This document captures the design intent, feature scope, technical approach, and
inter-module relationships for all planned LilithsEngine modules. It is the
authoritative reference for what each module is supposed to do before implementation begins.

> **Status key:** All modules below are PLANNED — not yet implemented.
> Active modules (Heart, Soul, Mind, Cookbook) are documented in ARCHITECTURE.md and CODE_MAP.md.

---

## Module Dependency Overview

```
LilithsHeart (required by all server modules)
  ├── LilithsCookbook      — recipes and stations (active)
  ├── LilithsArmory        — weapons and equipment
  ├── LilithsGrimoire      — spells and buffs
  ├── LilithsBounty        — drop tables
  ├── LilithsArchitects    — building recipes + schematics
  ├── LilithsAdversaries   — enemies, VBloods, wanted system
  ├── LilithsWisdom        — unlock gating
  ├── LilithsTreasury      — stash system
  ├── LilithsBlessings     — ritual buffs
  ├── LilithsConquest      — expeditions and PvP simulation
  ├── LilithsMenagerie     — creature breeding and production
  ├── LilithsMachinations  — quest system
  └── LilithsNexus         — teleportation

LilithsSoul (client — standalone, no Heart dependency at load time)
  ├── UI panels for: Treasury, Menagerie, Conquest, Blessings,
  │                  Machinations, Nexus, Architects, Adversaries
  └── Client feature areas (Soul-internal, no separate module):
        ├── Camera        — third person and first person modes
        ├── CeilingTiles  — structural floor tile mirroring for interior rendering
        └── Appearances   — per-character texture overrides (pairs with Heart's
                            AppearanceSync feature area for broadcast support)

Heart Appearance Feature Area (paired with Soul Appearances, gated independently):
  └── AppearanceSync    — server-side storage and broadcast of player appearance data
```

**Cross-module communication rule:** All modules publish and subscribe to
`HeartEventBus` exclusively. No module holds a direct reference to another
module's classes. This preserves independent installability.

---

## Soul Client Features

These features live inside LilithsSoul (and partially LilithsHeart for server-paired
concerns) rather than as separate installable modules. The deciding factor is idle
performance cost — when a feature flag is off, zero overhead is incurred. Since Soul
runs on one machine for one player, absorbing optional client work here avoids adding
install friction for players. The modular philosophy applies primarily to server-side
modules where feature scope genuinely varies per server.

> All Soul client features are disabled by default (`changesEnabled = false` philosophy).
> Players opt in via `LilithsSoul.cfg` or the in-game options menu where applicable.

---

### Camera

**Role:** Adds third person and first person camera modes to V Rising's default
bird's eye perspective.

**Scope:**
- Third person mode — over-the-shoulder view with configurable shoulder offset
  (left / centre / right), zoom distance, FOV, and pitch angle
- First person mode — full immersive first person with configurable FOV
- Mode cycling via a single bindable hotkey (forward press / shift+press for reverse)
- Optional discrete per-mode hotkey binds
- All settings exposed in the game's native options menu (keybinds + sliders)
- Last-used camera mode persists locally across sessions and servers

**Technical approach:** Harmony patch on V Rising's camera update method to intercept
and substitute position/rotation calculations. RetroCam (existing mod) proves these
hooks are accessible — study their implementation before writing our own. In-game
options and keybind registration API to be confirmed via RetroCam source inspection.

**Config (LilithsSoul.cfg):**
```
CameraEnabled = false
DefaultCameraMode = BirdsEye       # BirdsEye | ThirdPerson | FirstPerson
ThirdPersonFOV = 70
ThirdPersonZoom = 8
ThirdPersonPitch = 15
ThirdPersonShoulderOffset = Right   # Left | Centre | Right
FirstPersonFOV = 90
CameraTransitionsEnabled = true
```

**Future experiment (not in scope yet):**
Auto-swap shoulder offset when targeting an enemy — noted for later investigation.

**Soul infrastructure dependency:** `SoulOptionsRegistry` — registers keybinds and
settings sliders with the game's native options UI. Shared by all Soul client features
that expose player-facing controls. Implementation requires RetroCam API research.

**Performance:** No per-frame overhead when `CameraEnabled = false` — no patches
registered. When enabled, only the camera update intercept runs per frame, which is
minimal. No ECS queries, no allocations.

---

### CeilingTiles

**Role:** Renders mirrored structural floor tiles as ceiling tiles for improved
interior immersion and screenshot quality, particularly in first person and
low-angle third person.

**Scope:**
- Mirrors only structural floor grid tiles — decorative tiles, rugs, blood pools
  excluded
- Configurable horizontal radius (tile distance outward from player XZ position)
- Configurable vertical layers (how many floors above to render simultaneously)
- Both axes independently adjustable via hotkeys on the fly
- On-screen toast notification shows current radius and layer values when adjusted
- Master on/off hotkey toggle — players with weaker machines can enable for
  screenshots only without committing to full-time rendering cost
- Auto-detection of castle territory boundary to activate/deactivate automatically
  (manual toggle always overrides)
- Ceiling tile state (on/off, radius, layers) persists locally between sessions

**Hotkeys (all bindable via SoulOptionsRegistry):**
```
CeilingTileToggle          — master on/off
Page Up                    — increase horizontal radius by 1
Page Down                  — decrease horizontal radius by 1
Shift + Page Up            — increase vertical layers by 1
Shift + Page Down          — decrease vertical layers by 1
```

**Config (LilithsSoul.cfg):**
```
CeilingTilesDefaultEnabled = false
CeilingTileAutoDetectCastle = true     # auto toggle on castle territory entry/exit
CeilingTileHorizontalRadius = 5
CeilingTileVerticalLayers = 1
CeilingTileHorizontalRadiusMax = 20
CeilingTileVerticalLayersMax = 6
```

**Technical approach — two-path rendering strategy:**

Primary path (occlusion-driven, preferred): Hook into Unity's renderer visibility
callbacks (`OnBecameVisible` / `OnBecameInvisible` on floor tile renderers, or
`Renderer.isVisible` polling) to spawn/despawn ceiling tiles only for floor tiles
currently visible to the camera. Zero spatial math — mirrors the renderer's own
culling decisions exactly.

Fallback path (radius-driven): If occlusion hooks are not accessible on floor tile
GameObjects, maintain a tile grid within the configured radius. Grid rebuilds only
when the player crosses a tile boundary — not per frame.

```
CeilingTileMode = Auto     # Auto | OcclusionDriven | RadiusDriven
```

`Auto` attempts occlusion-driven first and silently falls back to radius-driven.
Players never need to know which path is active.

Ceiling tiles are placed at the same transform as their source floor tile, flipped
on the Y axis only. No additional vertical offset — floor tile geometry is planar
so the underside is invisible and exact positioning is effectively free. Normal map
behaviour when flipped is an open implementation-phase question; texture override
system can address this if needed.

**Performance:** V Rising's hard cap of 600 floor tiles per castle means worst case
is 1200 total tile GameObjects (floor + ceiling). In practice the radius system
ensures far fewer are active simultaneously. High `HorizontalRadiusMax` combined
with high `VerticalLayersMax` is the most expensive configuration and should be
documented clearly. Grid rebuilds are boundary-triggered, never per-frame.
`CeilingTilesDefaultEnabled = false` means zero cost when the feature is off —
no hooks registered, no GameObjects, no position tracking.

**Open implementation questions:**
- Which occlusion hook is accessible on V Rising floor tile GameObjects (needs
  runtime scene hierarchy inspection)
- Normal map appearance of Y-flipped tiles (verify at implementation; texture
  override system available as fallback fix)
- Consistent ceiling height across all castle piece types (verify at implementation)

---

### Appearances

**Role:** Per-character texture overrides for body, head, hair, armor, and weapon
slots. Allows players to personalise their character's visual appearance beyond
vanilla options. Server-paired feature: Heart stores and broadcasts appearance data
so other players can see each other's overrides.

**Design principle:** Heart does the minimum necessary to be the authority. Soul
does all fetching, caching, and rendering work. Heart never performs HTTP requests,
texture loading, or per-frame appearance checks.

---

#### Appearance Slots

| Slot | Notes |
|------|-------|
| `Body` | Full body texture |
| `Head_01`, `Head_02`, `Head_03`... | One slot per head variant. Textures are only compatible with the matching head variant — UI shows only compatible options for the character's current head |
| `Hair` | |
| `HairAccessory` | |
| `FacialAccessory` | |
| `Chest` | |
| `Gloves` | |
| `Legs` | |
| `Boots` | |
| `Headpiece` | |
| `Weapon_<type>` | One slot per weapon type (e.g. `Weapon_Sword`, `Weapon_Scythe`). Override applies to that weapon type regardless of tier. Only the N most recently used weapon type overrides are sent to the server (N is server-configured, default 5) |

Weapon overrides are tracked separately from character presets — the server
communicates the maximum weapon appearance count it supports; Soul sends only
the most recently used N weapon type appearances accordingly.

---

#### Texture Tiers

**Bundled styles (Tier 1):** Curated textures authored by the suite creator, shipped
inside Soul's install. Referenced by a stable `StyleId` string known to both Soul
and Mind. Always available with no external dependency. Stored under
`LilithsHeart/Appearances/Styles/` with a `styles_manifest.json` index.
Soul carries its own copy of the manifest for the local appearance editor UI —
Heart does not need to sync bundled style definitions.

**Custom URL textures (Tier 2):** Player or server provides an external URL pointing
to a texture file. Soul fetches, caches locally, and applies. Enables community
sharing of texture packs. Gated independently from bundled styles at both server
and client level.

---

#### Preset System

Players author named appearance presets stored locally in Soul's cache. Each preset
defines the full slot configuration for a character.

```json
{
  "ActivePreset": "Evening Look",
  "Presets": [
    {
      "Name": "Evening Look",
      "Body": { "StyleId": "PaleRose_01" },
      "Head_02": { "StyleId": "RedLips_01" },
      "Hair": null,
      "Chest": { "Url": "https://example.com/mythic_chest.png" },
      "Weapon_Sword": { "StyleId": "ObsidianBlade_01" }
    },
    {
      "Name": "Battle Ready",
      "Body": { "StyleId": "WarPaint_02" }
    }
  ]
}
```

- `StyleId` and `Url` are mutually exclusive per slot. `StyleId` wins if both present.
- Only the `ActivePreset`'s resolved slot state is broadcast to Heart — preset names,
  counts, and inactive presets are private to the local client file.
- Client may save unlimited presets locally.
- Server stores up to N presets per player (server-configurable, default 4). Player
  must delete server-side presets to free space before saving new ones.
- Appearance changes require a save/apply step in the UI — no live preview broadcast.
- The appearance panel is visible to all players. Broadcasting to other players is
  the gated part, not the panel itself.

---

#### Directory Structure

```
LilithsHeart/Appearances/
├── Permissions/
│   └── approved_players.json        — server whitelist and mode config
├── Styles/                          — bundled curated textures (shipped with Soul)
│   ├── Face/
│   ├── Body/
│   ├── Nails/
│   └── styles_manifest.json         — index of all bundled styles, display names, slot assignments
└── Custom/                          — per-player server-side appearance data
    └── <SteamId>/
        └── appearance.json          — active preset slot state for this player
```

Soul-side cache (local to the player's machine, per server identity):
```
LilithsSoul/Cache/<ServerIdentity>/
├── appearance_whitelist.json        — client-side personal whitelist
└── AppearanceCache/                 — locally cached URL textures
    └── <url_hash>.png
```

---

#### Permission Model

**Server-side broadcast mode (HeartConfig):**

| Setting | Behaviour |
|---------|-----------|
| `AppearanceSyncMode = Permissive` | All players may broadcast appearances; admins revoke individuals |
| `AppearanceSyncMode = Whitelist` | No player may broadcast until explicitly approved in `approved_players.json` |

**Per-player flags (approved_players.json):**
- `CanSetAppearance` — may this player set and broadcast their own appearance
- `CanUseCustomUrls` — may this player use external URL textures specifically

Defaults: both `true` in Permissive mode, both `false` in Whitelist mode until granted.

If a player's `CanSetAppearance` is revoked mid-session, Heart broadcasts a clear
appearance event for that character to all clients immediately — no wait for next
reconnect.

**approved_players.json entry format:**
```json
{
  "SteamId": "76561198012345678",
  "PlayerName": "Seraphine",
  "CanSetAppearance": true,
  "CanUseCustomUrls": false
}
```

SteamId is the canonical identifier. PlayerName is human-readable convenience for
file editing and command use. If name and SteamId conflict (player renamed), SteamId
wins and the file self-heals the name field on next write. Commands accept either
SteamId or PlayerName as input, resolving via the same three-path lookup pattern
used by PrefabNameResolver.

**Client-side personal whitelist (Soul):**
Players maintain a local whitelist of SteamIds whose custom appearances they consent
to rendering. Soul receives all appearance broadcasts from Heart regardless — filtering
happens at the applicator level on the client. Stored per server identity so preferences
are independent per server.

```json
{
  "SteamId": "76561198087654321",
  "PlayerName": "Morrigan"
}
```

Entries resolved by SteamId first. PlayerName enriched from Heart broadcast data
when available.

**Client-side tier toggles (SoulConfig):**
```
AppearancesEnabled = false           # master toggle — receive and render appearances at all
CustomAppearancesEnabled = false     # render custom URL textures from other players
```

**Server-side tier toggles (HeartConfig):**
```
AppearanceSyncEnabled = false        # master toggle — store and broadcast appearance data at all
CustomAppearanceSyncEnabled = false  # accept and broadcast custom URL texture entries
AppearanceSyncMode = Permissive      # Permissive | Whitelist
MaxPresetsPerPlayer = 4
MaxWeaponAppearances = 5             # how many recent weapon type overrides to request from clients
AppearanceChangeCooldownSeconds = 30
```

---

#### Sync Architecture

Appearance sync is a **fully isolated parallel channel** — completely separate from
the existing `SyncPayloadCache`, `SyncSender`, and `SyncQueue`. When
`AppearanceSyncEnabled = false` none of the appearance infrastructure initialises.
The existing sync pipeline is untouched regardless.

**Heart-side components (appearance feature area):**
- `AppearanceStore` — reads/writes `Custom/<SteamId>/appearance.json` per player
- `AppearanceSyncSender` — broadcasts appearance payloads via `[[LE::appearance:...]]`
  sentinels, handled in `ServerChatSystemPatch` (the single home for all Soul→Heart
  and Heart→Soul sentinel communication)
- Triggers: player connects → broadcast that player's appearance to all online clients;
  player updates appearance (and cooldown has elapsed) → broadcast delta to all clients;
  permission revoked → broadcast clear event to all clients

**Cooldown enforcement:**
- Client sends appearance update sentinel to Heart
- Heart checks cooldown per player. If not elapsed: responds with current cooldown
  remaining value via sentinel — Soul reads this, stores the value locally, and
  suppresses resends until elapsed
- Heart does one check and one conditional response — no polling, no per-frame work

**Soul-side components (appearance feature area):**
- `AppearanceSyncReceiver` — listens for `[[LE::appearance:...]]` sentinels,
  maintains in-memory `SteamId → AppearanceData` map
- `AppearanceTextureCache` — disk-backed cache for URL textures (keyed by URL hash),
  memory cache for bundled style textures. Lazy load — textures fetched only when
  a character using them enters render range
- `AppearanceApplicator` — hooks character entity spawn/despawn to apply and release
  texture overrides via `Renderer.material.SetTexture()`
- `AppearanceWhitelistService` — filters applicator output against the client-side
  personal whitelist and the `CustomAppearancesEnabled` flag

**Performance:** Soul is responsible for all texture fetching (HTTP), caching, and
application. Heart never performs HTTP requests or texture work. URL textures are
lazy-loaded and cached to disk — the second encounter of any URL is instant.
`AppearancesEnabled = false` means zero hooks registered, zero memory held, zero
per-frame cost.

---

## LilithsArmory

**Role:** Weapon and equipment stat configuration. The Cookbook parallel for gear.

**Scope:**
- Weapon damage, attack speed, reach, and special property overrides
- Armor stat overrides (physical/spell resistance, bonus stats)
- Accessory stat overrides
- All configuration via JSON, same authoring pattern as Cookbook

**Technical approach:** Prefab entity component patching via PrefabNameResolver.
Same two-pass pattern as Cookbook (prefab entities + live entities).

**Soul obligations:** Client-side prefab patches for UI display of weapon/armor stats.
Mirrors the same Heart patches so tooltips reflect server values.

**Inter-module relationships:**
- LilithsWisdom can gate equipping or unlocking specific gear tiers
- LilithsBounty drop tables reference Armory-configured item GUIDs

---

## LilithsGrimoire

**Role:** Spell, buff, cooldown, and jewel trait configuration.
The Cookbook/Armory parallel for the magic system.

**Scope:**
- Spell stat overrides: damage, cooldown, cast time, range, AOE, charge count, blood cost
- Buff stat overrides: duration, stack limits, tick rate, tick damage/healing, stat modifier magnitudes
- Buff application rules: which spells apply which buffs, buff chains, conditional applications
- Jewel trait overrides: what trait a jewel provides for a given spell, magnitude of bonuses

**Technical approach:** Prefab entity component patching against spell and buff prefab entities.
Component names (`AbilityData`, `CooldownData`, `BuffData`, etc.) need verification against
V Rising assemblies. Jewel trait component structure is an open investigation item.

**Soul obligations:** Client-side patches for spell bar cooldown display, buff bar duration
display, spellbook UI descriptions, and hover tooltips.
Localization injection handles display name and description text changes.

**Inter-module relationships:**
- LilithsWisdom gates which spells a player can unlock/use
- LilithsBlessings ritual buffs may be defined as Grimoire buff entries
- LilithsAdversaries enemy spell configuration references Grimoire spell definitions

**Open questions:**
- Jewel trait component structure in V Rising assemblies (needs investigation)
- Whether spell stat display in tooltips is read from components or baked into localization strings

---

## LilithsBounty

**Role:** Drop table configuration for enemies, resources, and chests.

**Scope:**
- Enemy unit drop table overrides (common drops, rare drops, guaranteed drops, blood quality)
- VBlood-specific drop overrides
- Resource node drop overrides (wood, stone, ore, plants, fish)
- Chest and container loot table overrides
- Drop quantity ranges and chance weights
- Integration with stash system: drops can credit stash items directly

**Technical approach:** Drop tables in V Rising are almost certainly buffer components
on prefab entities (same pattern as recipe buffers). Identify the relevant buffer type,
patch quantities and item references from JSON config.

**Inter-module relationships:**
- LilithsTreasury: drops can credit stash currencies or semantic item variants directly
- LilithsMenagerie: resource node drops can include capturable creature entries
- LilithsAdversaries: Adversaries configures enemy units; Bounty configures what they drop

---

## LilithsArchitects

**Role:** Castle building recipe configuration and schematic-based quick-build.

**Scope:**
- Building material requirement overrides per tile/object type
- Blood essence cost overrides
- Decay rate and upkeep cost overrides
- Repair cost overrides
- LilithsWisdom integration: specific building types gated behind unlock conditions
- Schematic placement via Kindred Schematics integration

**Schematic system:**
- Schematics are JSON definitions: tile/object types + relative positions + material cost
- Admin authors schematics via in-game recording (`.schematic record start/stop`) or manual JSON
- Players select a schematic at the Castle Planning Table (proximity panel)
- Heart validates material requirements, deducts from inventory, calls Kindred Schematics placement

**Kindred Schematics integration (soft dependency):**
```csharp
if (KindredSchematicsCompat.IsAvailable)
    KindredSchematicsCompat.PlaceSchematic(schematicName, origin, ownerEntity);
else
    // Graceful degradation: provide material kit, player builds manually
```
Full schematic placement requires Kindred Schematics to be installed.
Building recipe configuration works independently without it.

**Soul obligations:** Castle Planning Table proximity panel — schematic browser,
material cost display, build confirmation.

**Inter-module relationships:**
- LilithsWisdom can gate specific building types or schematics behind unlock conditions
- LilithsTreasury: schematic build cost can include stash currencies

**Open questions:**
- Exact building component names for material/upkeep config (needs assembly verification)
- Kindred Schematics API surface (refer to Kindred source for integration details)

---

## LilithsAdversaries

**Role:** Enemy and VBlood configuration, faction wanted system, and NPC castle sieges.

### Layer 1 — Enemy and VBlood Configuration

**Scope:**
- Unit stat overrides: health, speed, damage, resistances, weaknesses
- Blood type and blood quality on kill (feeds into blood system / Bloodcraft compat)
- VBlood phase thresholds, phase-specific ability sets, enrage conditions
- VBlood unlock rewards on kill (coordinates with LilithsWisdom)
- Enemy ability set configuration (references LilithsGrimoire spell definitions)
- Drop table coordination with LilithsBounty

### Layer 2 — Faction Wanted System

**Scope:**
Per-player, per-faction infamy tracking with escalating responses.

```
Factions: Militia, Church of Light, Bandits, Werewolves,
          Undead Legion, Harpy Clan, etc. (server-configurable)

Infamy sources: unit kills, camp raids, VBlood kills, ritual completions
Infamy decay: configurable per faction, per tier (real-time decay)

Wanted tiers (example defaults):
  Tier 1 (Noticed)   — increased patrol aggression near camps
  Tier 2 (Wanted)    — bounty hunters spawn and pursue player
  Tier 3 (Notorious) — elite hunting parties dispatched
  Tier 4 (Nemesis)   — named champion spawned, actively hunts player
```

Named champion: a procedurally or config-named unit with stats scaled to player
infamy level. Defeating the champion resets infamy to Tier 2 and starts decay.

**Technical approach:** Infamy store is pure managed C# `Dictionary<ulong, Dictionary<string, int>>`
(SteamID → faction → infamy value). Persisted to JSON. Kill/action events come from
HeartEventBus. Bounty hunter and elite party spawning uses `EntityManager.Instantiate`
+ position write.

### Layer 3 — NPC Castle Sieges

**Scope:**
High-infamy factions organise sieges against the offending player's castle.

Two implementation tiers:
- **Simulated siege (v1):** Heart calculates outcome based on castle defence rating
  vs faction siege strength. Player receives notification, timer, and result.
  No real units spawned. Same simulation approach as LilithsConquest.
- **Real siege (aspirational):** Heart spawns enemy units at castle perimeter.
  Units path toward castle heart using vanilla pathfinding. Requires investigation
  of whether vanilla pathfinding handles NPC-spawned units targeting castle entities.

**Soul obligations:** Infamy display (HUD indicator or panel tab), incoming siege
notification panel, named champion alert.

**Inter-module relationships:**
- LilithsBounty: faction drops LilithsBounty-configured loot on defeat
- LilithsMachinations: defeating a champion or surviving a siege can be a quest objective
- LilithsBlessings: completing a siege can contribute to ritual progress
- LilithsWisdom: defeating a champion can trigger unlock conditions
- LilithsNexus: high infamy can lock player out of faction-controlled waygate locations
- LilithsTreasury: bounty hunters drop faction-specific stash currencies

**Open questions:**
- NPC pathfinding toward castle heart for real sieges (needs investigation)
- VBlood phase component names (needs assembly verification)

---

## LilithsWisdom

**Role:** Per-player conditional unlock gating for recipes and spells.

**Scope:**
- Recipe unlock conditions: VBlood kill, quest completion, item use, ritual completion,
  infamy threshold, expedition completion, creature breed, or any HeartEventBus event
- Spell unlock conditions: same condition types
- Unlock state is per-player — one player unlocking does not affect others
- Unlock state persisted to JSON per player
- Integrates with `PlayerRecipesToAdd/Remove` in the sync payload for recipe delivery
- Spell unlock delivery mechanism: TBD pending investigation of spell unlock ECS components

**Technical approach:** Wisdom listens to HeartEventBus for all relevant unlock trigger
events. On trigger: evaluates conditions, marks unlock in player store, calls
`Heart.RegisterPlayerRecipeChanges()` for recipe unlocks, sends targeted Soul
notification of new unlock.

**Config example:**
```json
{
  "UnlockKey": "Recipe_AlchemyTable_T02",
  "Type": "Recipe",
  "Conditions": [
    { "Type": "VBloodKill", "Target": "CHAR_Gorecrusher_VBlood" },
    { "Type": "QuestComplete", "QuestKey": "PathOfShadows" }
  ],
  "RequireAll": true
}
```

**Inter-module relationships:**
- Produces: recipe unlocks (via Heart), spell unlocks (via TBD mechanism)
- Consumes events from: Adversaries, Menagerie, Conquest, Blessings, Machinations, Nexus
- LilithsMachinations quest completion is a primary Wisdom trigger source

**Open questions:**
- Spell unlock state ECS component names and writability (needs assembly verification)

---

## LilithsTreasury

**Role:** Semantic stash system — per-player item stores outside vanilla ECS,
custom item variants, currencies, and magic aspects.

### Stash Stores

Two stores per player:

| | PlayerStash | CastleStash |
|--|-------------|-------------|
| Bound to | Player character | Castle heart entity |
| Access | Anywhere | Proximity to own castle |
| PvP death | Transferred to killer (configurable %) | Unaffected |
| PvE death | Configurable (lost / moved to CastleStash / per-category) | Unaffected |
| Clan sharing | No | Configurable |

### Stash Item Categories

**Semantic variants:** Subsets of a vanilla item. Have a BackingItem PrefabGUID and
optional ConvertRatio. Players convert real inventory items into stash variants and
redeem them back. Vanilla inventory sees only the base item type; Treasury tracks
which variant the player's supply represents.

```
Example: Oak and Birch as variants of Item_Resource_Wood
  Player has 200 Wood in vanilla inventory
  Runs: .stash convert Oak 100
  Result: Vanilla inventory 100 Wood + Stash: Oak x100
```

**Pure currencies:** No backing item. Granted by server events (bounty rewards,
quest completion, ritual grants, admin commands).

**Magic aspects:** Specialised currencies for spell/ritual/crafting gating.
Example: FireAspect, ShadowAspect converted from BloodEssence variants.

### PvP Death Transfer

On PvP kill: Heart detects kill source is another player character, transfers
victim's PlayerStash contents to killer's PlayerStash (configurable percentage).
Clan membership check prevents friendly-fire transfers.

### Convert/Redeem Safety

Write-ahead pattern: credit stash before consuming ECS inventory. Ensures no item
loss if server crashes mid-transaction.

### Custom Crafting

A server-defined crafting system using stash items as ingredients/outputs.
Entirely outside vanilla ECS — recipes are JSON config, validation is server-side.

```json
{
  "Key": "OakPlank",
  "DisplayName": "Oak Plank",
  "Requirements": [
    { "Item": "Oak", "Amount": 10, "Source": "Stash" },
    { "Item": "Item_Resource_Sawdust", "Amount": 5, "Source": "Inventory" }
  ],
  "Outputs": [
    { "Item": "Item_Resource_Plank", "Amount": 5, "Source": "Inventory" },
    { "Item": "OakEssence", "Amount": 1, "Source": "Stash" }
  ]
}
```

Mixed-source recipes: ingredients and outputs can be stash items, vanilla ECS items,
or both. Heart validates and processes both sides.

**Soul obligations:**
- StashPanel: Unity UI grid panel opening alongside vanilla inventory hotkey.
  Populated from targeted StashPayload. Displays stash items with icons, names, quantities.
- Custom crafting panel: recipe browser with stash + inventory requirement colouring.
  Availability checked client-side (stash from last payload + live ECS inventory read).
- CastleStashPanel: proximity-triggered panel at castle (Nexus stone or dedicated object).
- Silent VCF commands for all panel interactions (take, deposit, convert, redeem, craft).

**Inter-module relationships:**
- LilithsBounty: drops can credit stash items directly
- LilithsBlessings: rituals consume stash items as sacrifices
- LilithsConquest: expedition units are stash items; rewards credit stash
- LilithsMenagerie: creature units are stash items; production credits stash
- LilithsMachinations: quest rewards credit stash currencies
- LilithsAdversaries: bounty hunters drop faction stash currencies

---

## LilithsBlessings

**Role:** Ritual sacrifice system — applying temporary or permanent buffs to
players, clans, or the entire server via structured sacrifice recipes.

### Ritual Structure

A ritual has:
- **Sacrifices required:** any combination of vanilla items, stash items, and dominated unit kills
- **Progress tracking:** communal — multiple players can contribute; shared progress bar
- **Target scope:** individual player, entire clan, or entire server
- **Reward:** a buff (temporary with duration, or permanent reapplied on connect)

### Dominated Unit Sacrifice

Heart detects a dominated unit linked to the player's character entity and consumes
it as a ritual contribution. Exact component for dominated unit ownership needs
assembly verification.

### Buff Application

- Single player: standard buff entity linked to character entity
- Clan: iterate clan member entities, apply to each connected member
- Server: iterate all connected player entities

Permanent buffs: Heart reapplies on player connect (buff may not survive server restart).
Active ritual buffs stored in managed C# store, persisted to JSON.

**Soul obligations:** Ritual Altar proximity panel — active rituals with progress bars,
sacrifice contribution UI, completed ritual history.

**Inter-module relationships:**
- LilithsTreasury: ritual sacrifices can consume stash items
- LilithsGrimoire: ritual buff stats may be defined as Grimoire buff entries
- LilithsConquest: active ritual buffs can modify expedition unit stats
- LilithsWisdom: ritual completion can be a Wisdom unlock condition
- LilithsMachinations: ritual completion fires HeartEventBus event for quest objectives
- LilithsNexus: server-wide ritual completion can unlock temporary teleport locations
- LilithsAdversaries: dominated unit sacrifice requires a unit to be dominated (Adversaries territory)

---

## LilithsConquest

**Role:** Custom unit crafting, time-based expeditions, simulated PvP battles,
and servant mission configuration.

### Layer 1 — Servant Mission Configuration

Cookbook-style configuration for vanilla servant mission system:
- Mission duration overrides
- Required servant power level overrides
- Reward table overrides (coordinates with LilithsBounty)

Technical approach: same buffer component patching as Cookbook.
Mission reward buffer component names need assembly verification.

### Layer 2 — Custom Expeditions

Custom units are stash items — not real ECS entities. Defined as `UnitDef` config
entries with stat blocks (Attack, Defense, Speed, Health, SpecialAbility).

```
Crafting a unit: consume stash/vanilla items via custom crafting → credit unit stash item
Sending on expedition: debit units from stash → start server-side timer
On completion: Heart runs outcome simulation → return units (minus losses) → credit rewards
```

Expedition outcome simulation: unit stats vs mission difficulty, randomness factor,
special ability modifiers. Pure managed C# arithmetic.

### Layer 3 — Simulated PvP

```
Attacker selects target player + assigns attacking force from stash
Heart notifies defender → defender has configurable window to assign defenders
  └─ No response: auto-assign available units or apply undefended penalty
Heart runs battle simulation when window closes
  └─ Round-by-round calculation with randomness factor
  └─ Unit losses on both sides
  └─ Winner determined → consequences applied (stash transfers)
Heart sends battle report to both players
Soul renders post-battle summary panel
```

**Soul obligations:** Expedition Table proximity panel — available expeditions,
unit assignment, active expedition timers, battle report viewer.

**Inter-module relationships:**
- LilithsTreasury: units are stash items; expedition rewards credit stash
- LilithsBlessings: active ritual buffs modify unit combat stats
- LilithsWisdom: advanced unit types gated behind unlock conditions
- LilithsMachinations: expedition outcomes fire HeartEventBus events for quest objectives
- LilithsBounty: expedition completion can generate world bounty targets
- LilithsNexus: successful territory conquest can unlock temporary teleport locations
- LilithsMenagerie: trained crow expeditions can improve Conquest expedition outcome odds

---

## LilithsMenagerie

**Role:** Creature capture, breeding, training, and production loops for
horses, spiders, rats, and crows.

### Creature Data Model

Creatures are managed C# data records — not real ECS entities. Persisted to JSON.

```csharp
MenagerieCreature {
    CreatureId, Species, Breed, Name,
    Stats: Dictionary<string, float>,     // speed, health, silkRate, etc.
    Traits: List<string>,                  // "Venomous", "NightBlood", etc.
    Abilities: List<string>,
    CurrentActivity,                       // Idle, Breeding, Scouting, etc.
    ActivityEnd: DateTime,
    Parent1, Parent2,                      // breeding lineage
    Generation: int
}
```

### Breeding System

Offspring stats = average of parents + mutation roll. Higher generation = higher
stat ceiling potential + higher negative mutation risk. Trait inheritance: shared
traits pass to offspring; unique traits have percentage chance to pass; rare new
trait can appear. Each species/breed has configurable max stat ceilings.

### Species Production Loops

**Horses:**
- Saddlebag: carrying capacity stat → mobile stash slots accessible while mounted
- Mount/dismount: Heart detects via ECS component change, notifies Soul to show/hide saddlebag
- Combat abilities: bred traits give active/passive combat behaviours on the horse entity
- Horse stat bridge: on mount, Heart writes bred stats to real horse ECS entity components

**Spiders:**
- Passive production tick: silk type and quantity based on breed, traits, silk rate stat
- Silk variants: different breeds produce different silk types (credits stash items)
- Production stored in creature's buffer; player harvests via panel or command

**Rats:**
- Blood quality stat → feed buff potency when used as blood source
- Experimentation: consumes rat, produces research stash items (randomness based on traits)
- Breeding toward specific blood types

**Crows:**
- Training roles: Scout, Treasure Hunter, Messenger
- Expedition: assigned to area, returns after duration with results (intel or trinkets)
- Trinkets: small random stash item rewards
- Must be idle before reassignment

### Cross-Creature Interactions

- Crow expeditions can return with wild rat specimens of a specific blood type
- Rat traits housed near spiders can influence silk variant production (proximity mechanic)
- Spider silk feeds into custom crafting recipes for horse barding (stat improvement)
- Crow bonded to a horse improves Conquest expedition outcome odds for that player

### Horse Stat Bridge (Technical Challenge)

Highest complexity piece: bred horse stats must affect the real ECS horse entity
when the player mounts. Heart hooks mount event, finds player's current mount entity,
writes stat components from the creature record to the real entity. Exact component
names for horse speed/health need assembly verification. This bridges managed data
with live ECS state.

**Soul obligations:** Per-species proximity panels (Stables, Spider Loft, Rat Warren,
Crow Roost) or unified Menagerie panel with tabs. Breeding pair selection UI.
Production harvest UI. Crow expedition assignment UI.

**Inter-module relationships:**
- LilithsTreasury: creature units are stash items; silk/production output credits stash
- LilithsWisdom: advanced breeds gated behind unlock conditions
- LilithsMachinations: capture/breed events fire for quest objectives
- LilithsConquest: trained crows can augment expedition outcomes
- LilithsBounty: resource node drops can yield wild creature capture items

**Open questions:**
- Horse stat component names for speed/health/combat (needs assembly verification)
- Mount/dismount event detection mechanism (needs assembly verification)
- Dominated unit component for rat experimentation cross-reference

---

## LilithsMachinations

**Role:** Quest system — custom daily/weekly/repeatable quests, multi-step quest
chains, and main quest modification.

### Custom Quest Types

| Type | Reset behaviour |
|------|----------------|
| Daily | Resets at configured server time each day |
| Weekly | Resets at configured server day/time each week |
| Repeatable | Can be accepted and completed multiple times, no time gate |
| Chain | Multi-step; completing one step unlocks the next |

### Objective Types

Machinations listens to HeartEventBus for all objective tracking:

| Objective | Event source |
|-----------|-------------|
| Kill N enemies of type X | Adversaries kill events |
| Feed on N humans | Feed event hook |
| Craft N of item X | Cookbook craft events |
| Harvest N of resource X | Bounty harvest events |
| Defeat VBlood X | Adversaries VBlood kill events |
| Complete expedition | Conquest/Menagerie completion events |
| Reach infamy tier | Adversaries infamy events |
| Sacrifice item to ritual | Blessings ritual events |
| Capture/breed creature | Menagerie events |
| Visit location | Nexus proximity events |
| Complete N daily quests | Machinations meta-events |
| Convert/redeem stash items | Treasury events |

### Multi-Step Chains

Ordered objective groups. Completing the final objective in a step fires a
`ChainStepComplete` event on HeartEventBus, unlocking the next step. Rewards
can fire at each step or only on full chain completion.

### Reward Types

- Stash currency grant (LilithsTreasury)
- Recipe/spell unlock (LilithsWisdom)
- Ritual progress contribution (LilithsBlessings)
- Temporary teleport unlock (LilithsNexus)
- Infamy modification (LilithsAdversaries)
- Vanilla item spawn into inventory
- Custom notification + lore text delivery to Soul

### Main Quest Modification

| Surface | Feasibility | Notes |
|---------|-------------|-------|
| Quest display names and descriptions | ✅ Feasible | Localization injection already works |
| Objective requirements | ⚠️ Uncertain | Depends on component structure — needs investigation |
| Completion rewards | ⚠️ Uncertain | May be buffer components — needs investigation |
| Structural changes to quest flow | ❌ Not feasible | Hardcoded in Stunlock systems |

Main quest modification is approached cautiously. Text changes are safe.
Structural changes are out of scope.

**Soul obligations:** Notice Board proximity panel — available quests, active quest
tracking with objective progress bars, daily/weekly reset timers, completed quest history.
New unlock notifications (Wisdom-driven unlocks surfaced here).

**Inter-module relationships:**
- Machinations is the most downstream module — it consumes events from every other module
- Quest rewards feed into: Treasury, Wisdom, Blessings, Nexus, Adversaries
- HeartEventBus cross-module communication is critical for Machinations functionality

---

## LilithsNexus

**Role:** Teleportation — waygate network topology control, custom teleport
locations, personal waypoints, and temporary portals.

### Feature Set

**Waygate network control:**
Server-configurable which waygates connect to which. Topology options: one-way gates,
hub-only gates, gates restricted by player progression or infamy level.
Technical approach: waygate connection component investigation needed.

**Custom teleport locations:**
Entirely server-defined coordinates not tied to placed waygate entities.
Defined in Heart config with name, coordinates, access conditions.
Accessible via the Nexus panel.

**Personal waypoints:**
Player sets a personal recall point anywhere in the world. Only accessible to
that player. Configurable: cooldown duration, stash currency cost to set or use,
maximum number of saved waypoints per player. Persisted to JSON.

**Temporary portals:**
Time-limited or use-limited teleport destinations. Created by server events:
- Raid markers that expire after a session
- Bounty target locations that disappear on claim
- Ritual completion reward locations
- Conquest territory unlock locations

### Teleport Mechanics

Teleport operation: position component write on character entity + network sync event.
Well-understood in V Rising modding (Bloodcraft and others do this).

**Access gates:**
- Infamy check: high infamy with a faction blocks access to their controlled waygate
- Wisdom check: some locations require specific unlocks
- Stash currency cost: deducted by Heart before executing teleport

**Soul obligations:** Nexus Stone proximity panel — network gates tab, custom locations
tab, personal waypoints tab (set/delete/use), active temporary portals tab.

**Inter-module relationships:**
- LilithsBlessings: server-wide ritual completion creates temporary portal
- LilithsConquest: territory conquest creates temporary portal to that territory
- LilithsAdversaries: infamy gates access to faction-controlled waygate locations
- LilithsMachinations: visiting a location can be a quest objective; teleport events fire for tracking
- LilithsTreasury: waypoint set/use can cost stash currency

**Open questions:**
- Waygate network connection component structure (needs assembly verification)
- Whether modifying waygate connections requires client-side sync

---

## LilithsExpansion

Placeholder module slot for future ideas not yet defined.
Reserved plugin GUID: `audaciousbovine.lilithsexpansion`.

---

## Architectural Patterns Shared Across All Modules

### Proximity Panel Pattern
All custom UI panels are triggered by player proximity to a configured furniture
entity. See ARCHITECTURE.md — "Proximity Trigger System" for full details.

### Managed C# Store Pattern
All persistent per-player data (stash, creature records, quest progress, infamy,
personal waypoints, unlock state) lives in managed C# dictionaries persisted to
JSON. Never in ECS. This avoids serialisation conflicts, survives server restarts
cleanly, and has no prefab GUID constraints.

### Timer System Pattern
Time-based activities (expeditions, production ticks, infamy decay, temporary portals,
daily/weekly resets) use a shared server-side timer system. Heart checks elapsed time
on `ServerBootstrapSystem.OnUpdate` (or a dedicated tick patch) and fires completion
events via HeartEventBus.

### Soft Dependency Pattern
External mod integrations (Kindred Schematics, Bloodcraft) use runtime presence checks:
```csharp
if (KindredSchematicsCompat.IsAvailable)
    // enhanced path
else
    // graceful degradation
```
No hard BepInDependency on external mods. Modules function without the external mod present.