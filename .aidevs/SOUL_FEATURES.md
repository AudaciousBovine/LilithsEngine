# Soul Client Features — Design Reference

This document captures the design intent, feature scope, technical approach, and
open questions for all Soul-internal client feature areas. These features live inside
`LilithsSoul` (and partially `LilithsHeart` for server-paired concerns) rather than
as separate installable modules.

> **Status key:** All features below are PLANNED — not yet implemented.
> Active Soul infrastructure (sync, localization, LUI) is documented in ARCHITECTURE.md and CODE_MAP.md.

---

## Why Soul-Internal Rather Than Separate Modules

The modular philosophy of LilithsEngine applies primarily to the server side, where
feature scope genuinely varies per server and admins need to pick and choose what
runs. On the client side, Soul is responsible only for itself — one plugin, one
player's machine.

The deciding factor for whether a client feature warrants a separate module is
**idle performance cost**. If a disabled feature flag incurs zero runtime overhead —
no patches registered, no hooks, no GameObjects, no per-frame work — there is no
reason to push it to a separate download and add install friction for players.

All Soul client features pass this test. Every feature is fully dormant when its
config flag is `false`. Players who never enable a feature pay no cost for its
presence in the DLL.

**Player install requirement remains:** BepInEx + LilithsSoul. No additional plugins
ever required for client-side features.

---

## Shared Infrastructure

All Soul client features depend on two foundational Soul classes that must be
implemented before any feature area begins:

### SoulEventBus

Soul-internal pub/sub bus. Mirrors `HeartEventBus` in naming, placement, and API
convention. Lives in `LilithsSoul/Foundation/SoulEventBus.cs`.

Feature areas communicate with each other and with Soul's UI layer exclusively via
`SoulEventBus`. No direct cross-feature references.

**EventKind ranges** — see `ARCHITECTURE.md` (SoulEventBus section) for the
reserved range table.

### SoulOptionsRegistry

Bridge between Soul's feature set and V Rising's native options menu and keybind
system. Lives in `LilithsSoul/Foundation/SoulOptionsRegistry.cs`.

All player-facing controls that should appear in the game's options UI register
through this class. Provides a unified query surface for current keybind state.

**Implementation dependency:** Requires research into V Rising's options and keybind
registration API. RetroCam (existing mod) uses this API for both keybinds and slider
settings — consult RetroCam source before implementing.

---

## Camera

**Entry point:** `LilithsSoul/Features/CameraFeature.cs`
**Config gate:** `SoulConfig.CameraEnabled` (default `false`)
**SoulEventBus range:** 100–199

### Role

Adds third person and first person camera modes to V Rising's default bird's eye
perspective. Players cycle between modes via hotkey. Each mode has independently
configurable parameters exposed in the game's native options menu.

### Camera Modes

| Mode | Description | V Rising default? |
|------|-------------|:-----------------:|
| `BirdsEye` | Top-down orthographic view | ✅ |
| `ThirdPerson` | Over-the-shoulder follow camera | — |
| `FirstPerson` | Full immersive first person | — |

`CameraFeature` adds `ThirdPerson` and `FirstPerson` only. `BirdsEye` is the game's
own camera — `CameraFeature` does not touch it, but a player cycling backward from
`BirdsEye` wraps to `FirstPerson` and forward wraps to `ThirdPerson`.

### Per-Mode Parameters

All parameters configurable via both `LilithsSoul.cfg` and the game's native options
menu (registered via `SoulOptionsRegistry`).

**ThirdPerson:**
```
ThirdPersonFOV = 70               # field of view in degrees
ThirdPersonZoom = 8               # distance from character
ThirdPersonPitch = 15             # vertical angle in degrees
ThirdPersonShoulderOffset = Right # Left | Centre | Right
```

**FirstPerson:**
```
FirstPersonFOV = 90
```

**Shared:**
```
CameraTransitionsEnabled = true   # smooth interpolation between modes
```

### Hotkeys

All registered via `SoulOptionsRegistry` — appear in game's native keybind list.

| Action | Default | Notes |
|--------|---------|-------|
| Cycle mode forward | Unbound | Steps BirdsEye → ThirdPerson → FirstPerson → BirdsEye |
| Cycle mode backward | Shift + cycle key | Steps reverse direction |
| Discrete ThirdPerson bind | Unbound | Optional; jumps directly to ThirdPerson |
| Discrete FirstPerson bind | Unbound | Optional; jumps directly to FirstPerson |
| Discrete BirdsEye bind | Unbound | Optional; jumps directly to BirdsEye |

### Mode Persistence

The active `CameraMode` is persisted locally to `SoulConfig` between sessions.
Persistence is per-machine, not per-server — whatever mode the player used last
on any server is restored on next launch regardless of which server they connect to.

### Combat and UI Behaviour

No automatic mode switching on combat start or UI open. It is the player's
responsibility to switch modes as desired. Map and inventory panels paint over
the camera view regardless of mode — no forced camera state when UI opens.

### Shoulder Offset

ThirdPerson supports three shoulder positions: `Left`, `Centre`, `Right`.
Changeable via the options menu or by cycling the ThirdPerson discrete keybind
with a modifier (exact modifier TBD at implementation).

**Future experiment (not in scope):** Auto-swap shoulder offset when the player
targets an enemy — noted for later investigation after core camera is stable.

### Technical Approach

Harmony postfix on V Rising's camera update method to intercept and substitute
position and rotation calculations. RetroCam (existing mod) proves these hooks
are accessible and well-understood — **study RetroCam's implementation before
writing camera hooks**. Do not reinvent the hook discovery process.

In-game options and keybind registration API confirmed accessible via RetroCam —
consult source before implementing `SoulOptionsRegistry`.

### Config Summary

```
# LilithsSoul.cfg — Camera section
CameraEnabled = false
DefaultCameraMode = BirdsEye        # BirdsEye | ThirdPerson | FirstPerson
ThirdPersonFOV = 70
ThirdPersonZoom = 8
ThirdPersonPitch = 15
ThirdPersonShoulderOffset = Right   # Left | Centre | Right
FirstPersonFOV = 90
CameraTransitionsEnabled = true
```

### Performance

No per-frame overhead when `CameraEnabled = false` — no patches registered, nothing
running. When enabled: one Harmony intercept on the camera update method per frame,
minimal arithmetic. No ECS queries, no allocations in the steady state.

### Open Questions

- Exact camera update method name and signature (confirm via RetroCam source or
  assembly inspection before implementing)
- `SoulOptionsRegistry` API surface for slider and keybind registration
  (confirm via RetroCam source)
- Smooth transition interpolation approach — lerp on position/rotation or
  Unity animation curve (decide at implementation)

---

## CeilingTiles

**Entry point:** `LilithsSoul/Features/CeilingTileFeature.cs`
**Config gate:** `SoulConfig.CeilingTilesDefaultEnabled` (default `false`)
**SoulEventBus range:** 200–299

### Role

Renders mirrored structural floor tiles as ceiling tiles, giving castle interiors
a finished appearance when viewed from first person or low-angle third person.
Primarily useful for immersion and screenshots. Not a continuous mandatory renderer
— designed to be toggled on demand.

### Scope

- Mirrors **structural floor grid tiles only** — decorative overlays (rugs, blood
  pools, painted tiles) are excluded
- All mirrored tiles use the same texture as their source floor tile
- Configurable horizontal radius (tile distance outward from player XZ position)
- Configurable vertical layers (how many floors above to render simultaneously)
- Both axes adjustable via hotkey on the fly with live on-screen feedback
- Master on/off hotkey toggle — players with weaker machines enable for screenshots
  only
- Castle territory auto-detection activates/deactivates the system automatically
  (manual toggle always overrides)
- State (on/off, radius, layers) persists locally between sessions

### Tile Transform

Ceiling tiles are placed at the same world position as their source floor tile,
flipped on the Y axis only. No additional vertical offset — floor tile geometry
is a plane and the underside is invisible, so exact Y positioning is effectively
free. Normal map appearance when flipped is an open implementation question;
the existing texture override system is available as a fallback fix if needed.

### Rendering Strategy — Two Paths

`CeilingTileMode` in config selects the rendering path. `Auto` is the default and
tries the primary path first, falling back silently if unavailable.

**Primary path — Occlusion-driven (preferred):**

Hook into Unity's renderer visibility events on floor tile GameObjects:
- `OnBecameVisible` / `OnBecameInvisible` MonoBehaviour callbacks, or
- `Renderer.isVisible` polling as an alternative

When a floor tile becomes visible to the camera → spawn a ceiling tile directly
above it. When it becomes invisible → despawn the ceiling tile. The ceiling tile
pool is always exactly the set of currently visible floor tiles — no spatial math,
no radius management, zero cost for tiles the camera cannot see.

**Fallback path — Radius-driven:**

If occlusion hooks are not accessible on V Rising's floor tile GameObjects, maintain
a tile grid within the configured radius around the player. Grid rebuilds only when
the player crosses a tile boundary — never per frame. A tile boundary crossing is
detected by a cheap integer position comparison each frame.

```
CeilingTileMode = Auto     # Auto | OcclusionDriven | RadiusDriven
```

Which path is viable cannot be determined until floor tile GameObjects are inspected
at runtime. A debug command that dumps nearby tile component info at implementation
time will resolve this quickly.

### Radius Configuration

Two independent axes give players precise control over the rendering cost/coverage
tradeoff:

```
CeilingTileHorizontalRadius = 5    # tile distance outward from player XZ (RadiusDriven / Auto fallback)
CeilingTileVerticalLayers = 1      # floors above to render simultaneously
CeilingTileHorizontalRadiusMax = 20
CeilingTileVerticalLayersMax = 6
```

`HorizontalRadius` handles large open rooms vs compact corridor castles. A player
with a grand hall sets a high value; a player with small rooms keeps it tight.

`VerticalLayers` has a non-linear performance impact since tile count multiplies per
layer. Document clearly that high vertical + high horizontal is the most expensive
configuration.

**Hard ceiling context:** V Rising's maximum floor tile count per castle is 600.
Worst case (all tiles visible, all layers rendered) is 1200 GameObjects — manageable.
In practice the radius system and camera culling always produce far fewer active tiles.

### Hotkeys

All registered via `SoulOptionsRegistry`.

| Action | Default |
|--------|---------|
| Master toggle (on/off) | Unbound |
| Horizontal radius + | Page Up |
| Horizontal radius − | Page Down |
| Vertical layers + | Shift + Page Up |
| Vertical layers − | Shift + Page Down |

### On-Screen Toast

When adjusting radius or layers via hotkey, a small unobtrusive toast notification
displays the current values momentarily and fades after ~2 seconds:

```
Ceiling Tiles: Horizontal 7  |  Vertical 2
```

Implemented as a Soul UI element on a high-order Canvas — no LUI dependency required
for this simple display.

### Castle Auto-Detection

V Rising already tracks castle territory boundaries internally (used for castle heart
mechanics, servant management, etc.). Hook into the existing boundary state rather
than performing an independent spatial query — we read a state the game already
maintains, not compute our own.

`CeilingTileAutoDetectCastle = true` (default): system activates on castle territory
entry and deactivates on exit. The manual hotkey toggle always overrides — a player
can force it on outside a castle for outdoor screenshots, or force it off inside a
castle for performance.

### Config Summary

```
# LilithsSoul.cfg — CeilingTiles section
CeilingTilesDefaultEnabled = false
CeilingTileMode = Auto              # Auto | OcclusionDriven | RadiusDriven
CeilingTileAutoDetectCastle = true
CeilingTileHorizontalRadius = 5
CeilingTileVerticalLayers = 1
CeilingTileHorizontalRadiusMax = 20
CeilingTileVerticalLayersMax = 6
```

### Performance

`CeilingTilesDefaultEnabled = false` — zero cost: no hooks, no GameObjects, no
position tracking, no per-frame work.

When enabled: tile boundary crossing detection is a cheap integer position comparison
each frame. Grid rebuilds are boundary-triggered only — never continuous. Occlusion
path has no spatial math at all; renderer callbacks are event-driven. URL texture
fetching never applies here — all ceiling tiles use the same source texture.

`[PERFORMANCE]` annotations required at implementation on: the boundary crossing
check, grid rebuild, and any per-frame occlusion polling fallback.

### Open Questions

- Which occlusion hook is accessible on V Rising floor tile GameObjects — requires
  runtime scene hierarchy inspection at implementation time. Dump nearby tile
  component info via a debug command to determine.
- Normal map appearance of Y-flipped tiles — verify at implementation. Texture
  override system available as fallback fix.
- Consistent ceiling height across all castle piece types — verify at implementation.
  If heights vary, `VerticalLayers` offset math will need per-piece-type height data.
- Exact castle territory boundary event or component to hook for auto-detection —
  confirm via assembly inspection.

---

## Appearances

**Entry point:** `LilithsSoul/Features/AppearanceFeature.cs`
**Config gate:** `SoulConfig.AppearancesEnabled` (default `false`)
**SoulEventBus range:** 300–399
**Heart paired feature:** `AppearanceSyncEnabled` in `HeartConfig` (default `false`)

### Role

Per-character texture overrides for body, head, hair, armor, and weapon slots.
Allows players to personalise their character's visual appearance beyond vanilla
options using curated bundled styles or community-shared external URL textures.

Server-paired: Heart stores active appearance presets and broadcasts them to other
clients so players can optionally see each other's overrides.

> **Implementation dependency:** The appearance panel UI requires LUI to be
> implemented first. The sync infrastructure and texture application can be
> built independently of LUI, but the player-facing preset management requires
> a functioning panel system.

### Design Principle

> Heart does the minimum necessary to be the authority.
> Soul does all fetching, caching, and rendering work.

Heart's appearance obligations: permission check, cooldown check, write to disk,
broadcast payload. Nothing more. Heart never performs HTTP requests, texture
loading, or per-frame appearance work.

---

### Appearance Slots

| Slot | Notes |
|------|-------|
| `Body` | Full body texture |
| `Head_01`, `Head_02`, `Head_03`... | One slot per head variant. Textures are only compatible with the matching head. UI shows only compatible options for the character's current head. |
| `Hair` | |
| `HairAccessory` | |
| `FacialAccessory` | |
| `Chest` | |
| `Gloves` | |
| `Legs` | |
| `Boots` | |
| `Headpiece` | |
| `Weapon_<type>` | One slot per weapon type (e.g. `Weapon_Sword`, `Weapon_Scythe`). Override applies to that weapon type regardless of tier or material. Only the N most recently used weapon type overrides are sent to server (N is server-configured, default 5). Weapon overrides tracked separately from presets. |

Exact head variant count and slot names require assembly inspection at implementation
time.

---

### Texture Tiers

**Tier 1 — Bundled styles:**
Curated textures authored by the suite creator, shipped inside Soul's install.
Referenced by a stable `StyleId` string (e.g. `"RedLips_01"`, `"GlitterNails_Blue"`).
Defined in `styles_manifest.json`. Always available, no external dependency, no
trust concern. Soul carries its own copy of the manifest for the appearance editor
UI — Heart never needs to sync it.

**Tier 2 — Custom URL textures:**
Player or server provides an external URL pointing to a texture file. Soul fetches,
caches locally, and applies. Enables community sharing. Gated independently from
bundled styles at both server and client level via `CustomAppearanceSyncEnabled`
(Heart) and `CustomAppearancesEnabled` (Soul).

`StyleId` and `Url` are mutually exclusive per slot. If both somehow present,
`StyleId` wins.

---

### Preset System

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

- Client may save **unlimited** presets locally
- Server stores up to `MaxPresetsPerPlayer` presets per player (server-configurable,
  default 4). Player must delete server-side presets to free space.
- Only the `ActivePreset`'s resolved slot state is broadcast to Heart — all other
  presets remain private to the client.
- Appearance changes require a **save/apply** step — no live preview broadcast.
- The appearance panel is visible to all players. Broadcasting is the gated part,
  not viewing the panel.

---

### Directory Structure

```
LilithsHeart/Appearances/
├── Permissions/
│   └── approved_players.json          — server whitelist and mode config
├── Styles/                            — bundled curated textures (shipped with Soul)
│   ├── Face/
│   ├── Body/
│   ├── Nails/
│   └── styles_manifest.json           — index of all bundled styles with slot assignments
└── Custom/                            — per-player server-side appearance data
    └── <SteamId>/
        └── appearance.json            — active preset slot state for this player

LilithsSoul/Cache/<ServerIdentity>/
├── appearance_whitelist.json          — client personal whitelist (per server)
└── AppearanceCache/
    └── <url_hash>.png                 — cached URL textures (keyed by URL hash)
```

---

### Permission Model

#### Server-Side Broadcast Mode

Configured in `HeartConfig.AppearanceSyncMode`:

| Mode | Behaviour |
|------|-----------|
| `Permissive` | All players may broadcast. Admins revoke individuals. |
| `Whitelist` | No player may broadcast until explicitly approved in `approved_players.json`. |

#### Per-Player Flags (`approved_players.json`)

| Flag | Meaning | Default in Permissive | Default in Whitelist |
|------|---------|:---------------------:|:--------------------:|
| `CanSetAppearance` | May broadcast own appearance | `true` | `false` |
| `CanUseCustomUrls` | May use external URL textures | `true` | `false` |

#### `approved_players.json` Entry Format

```json
{
  "SteamId": "76561198012345678",
  "PlayerName": "Seraphine",
  "CanSetAppearance": true,
  "CanUseCustomUrls": false
}
```

**Identifier resolution:** SteamId is canonical. PlayerName is a human-readable
convenience for file editing and commands. If a player renames, SteamId wins on
any conflict and PlayerName self-heals on the next file write. Commands accept
either SteamId or PlayerName, resolved via the same three-path lookup pattern
used by `PrefabNameResolver`.

#### Mid-Session Permission Revocation

When `CanSetAppearance` is revoked for a player mid-session, Heart immediately
broadcasts a clear event to all online clients. Their `AppearanceApplicator`
releases that player's texture overrides at once — no wait for reconnect.

#### Client-Side Personal Whitelist

Players maintain a local list of SteamIds whose custom appearances they consent
to rendering. Soul receives all appearance broadcasts from Heart regardless — the
whitelist is applied at the `AppearanceApplicator` level on the client. Entirely
local with no server round-trips.

```json
{
  "SteamId": "76561198087654321",
  "PlayerName": "Morrigan"
}
```

PlayerName enriched from Heart broadcast data when available. Whitelist stored
per server identity — preferences are independent per server.

---

### Config Summary

**LilithsSoul.cfg:**
```
AppearancesEnabled = false            # master toggle
CustomAppearancesEnabled = false      # render URL textures from other players
```

**LilithsHeart.cfg (Appearances section):**
```
AppearanceSyncEnabled = false         # master toggle — store and broadcast at all
CustomAppearanceSyncEnabled = false   # accept and broadcast URL texture entries
AppearanceSyncMode = Permissive       # Permissive | Whitelist
MaxPresetsPerPlayer = 4
MaxWeaponAppearances = 5              # recent weapon type overrides to request from clients
AppearanceChangeCooldownSeconds = 30
```

---

### Sync Architecture

Appearance sync is a **fully isolated parallel channel** — see `ARCHITECTURE.md`
(Appearance Sync section) for complete flow diagrams, class tables, and sentinel
format specifications.

Summary:
- Shares only `ServerChatSystemPatch` as the sentinel intercept point
- `SyncPayloadCache`, `SyncSender`, `SyncQueue`, `SyncReceiver` are completely untouched
- Heart-side: `AppearanceStore`, `AppearanceSyncSender`, `AppearancePermissionService`
- Soul-side: `AppearanceSyncReceiver`, `AppearanceTextureCache`, `AppearanceApplicator`,
  `AppearanceWhitelistService`
- Cooldown: Heart checks and responds with remaining seconds; Soul suppresses resends locally
- Permission revocation: Heart broadcasts clear event immediately; Soul releases textures at once

#### Sentinel Family

**Soul → Heart:**
```
[[LE::appearance:update:<payload>]]    — submit active preset
[[LE::appearance:clear]]               — clear own appearance
```

**Heart → Soul:**
```
[[LE::appearance:data:<steamid>:<payload>]]   — full snapshot for a player
[[LE::appearance:clear:<steamid>]]            — remove a player's appearance
[[LE::appearance:cooldown:<seconds>]]         — cooldown remaining (to sender only)
[[LE::appearance:maxweapons:<n>]]             — MaxWeaponAppearances (sent on connect)
```

---

### Performance

`AppearancesEnabled = false` — zero cost: no hooks, no memory, no per-frame work.

When enabled:
- Heart: one permission + cooldown check per update request, one broadcast per change.
  No HTTP, no texture work, no polling.
- Soul: texture application on character entity spawn/despawn (event-driven, not
  per-frame). URL textures lazy-loaded and disk-cached by URL hash — second encounter
  is always instant. Memory cache holds only currently rendered players' textures;
  releases on despawn.

`[PERFORMANCE]` annotations required at implementation on: `AppearanceApplicator`
spawn/despawn hooks, `AppearanceTextureCache` fetch and cache operations.

### Open Questions

- Exact head variant count and `Head_N` slot names — requires assembly inspection
  at implementation time
- `Renderer.material.SetTexture()` accessibility on V Rising character GameObjects —
  confirm at implementation; well-understood pattern in Unity modding generally
- Exact character entity → GameObject resolution path for `AppearanceApplicator`
  (how to get the Unity renderer from the ECS entity) — confirm via scene inspection
  at implementation

---

## Feature Interaction Matrix

| | Camera | CeilingTiles | Appearances |
|---|:---:|:---:|:---:|
| **Camera** | — | CeilingTiles activates only when camera is in a supported mode (any); no direct dependency | No dependency |
| **CeilingTiles** | Reads `CameraMode` from `SoulEventBus` to inform auto-detect relevance | — | No dependency |
| **Appearances** | No dependency | No dependency | — |

All three features are functionally independent. CeilingTiles listens to
`SoulEventBus` for camera mode change events as an optional optimisation —
it could suppress ceiling tile rendering entirely when in bird's eye view
since the angle makes them invisible regardless. This is an implementation-phase
decision, not a design requirement.
