# Glossary

## Project Terminology

| Term | Definition |
|------|------------|
| **LilithsEngine** | The overall mod suite name. Thematic naming: Heart (server core), Soul (client core), Mind (shared knowledge), Cookbook (recipe module). |
| **Heart** | `LilithsHeart` — server-side plugin that manages ECS access, module registration, and sync payload delivery. |
| **Soul** | `LilithsSoul` — client-side plugin that intercepts chat messages, patches local ECS entities, injects localization, and renders custom UI panels. |
| **Mind** | `LilithsMind` — shared C# library with zero game dependencies. Holds prefab definitions, network DTOs, and shared enums. |
| **Cookbook** | `LilithsCookbook` — server-side child module of Heart that reads JSON config files and applies recipe, station, prisoner feed, and item function changes. |
| **Child Module** | A BepInEx plugin that depends on Heart (`[BepInDependency("audaciousbovine.lilithsheart")]`) and registers via `HeartModuleRegistry`. |
| **LilithItemConfig** | The shared in-memory data surface populated by `ItemService`. One dictionary keyed by prefab name, valued by `LilithItemData`. All item overrides (appearance + functional) live here. Downstream services read from it — no service except ItemService writes to it. |
| **ItemService** | The single owner of all `Items/*.json` file I/O in Heart. Parses all item fields into `LilithItemConfig` in one pass. All other item-related services are pure apply-layers. |
| **LocalizationFileService** | Loads per-language item name/description overrides from `Localization/<LanguageCode>/` subfolders at world ready. Results are served on demand by `LocalizationSyncSender`. |
| **SyncMode** | The delivery mechanism for the sync payload. `ChunkPush` (default, chat-based chunks), `HttpServer` (Heart hosts HTTP endpoint), `StaticUrl` (admin-hosted URL). Configured in `HeartConfig.SyncMode`. |
| **ServerLanguage** | The language code of the `DisplayName`/`DescriptionText` values in `ServerSyncPayload.ItemAppearanceOverrides`. Defaults to `"English"`. Soul compares this against `PreferredLanguage` to decide whether to request a localization payload. |
| **PreferredLanguage** | Soul-side setting (`SoulConfig.PreferredLanguage`). If it differs from `ServerLanguage`, Soul sends `[[LE::lang-request:X]]` to Heart after receiving the Critical tier. |
| **LocalizationSyncPayload** | A `ServerSyncPayload` with only `ItemAppearanceOverrides` populated (DisplayName + DescriptionText, no Icon, no StackSize). Sent by `LocalizationSyncSender` when Soul requests a non-default language. Cached to `localization_<language>.json` on the client. |
| **Alias** | An admin-defined short name for a prefab, stored in `Aliases/<IndexClassName>.json`. Overrides the compiled `Name` field from LilithsMind on a per-server basis. Resolved by `PrefabNameResolver` as the first lookup path. |
| **GenerateNameAliasConfigs** | HeartConfig flag that dumps compiled `Name` defaults to `Aliases/*.json` so admins have a starting point for customization. Always overwrites. |
| **ChangesEnabled** | A bool field on `LilithItemData` that gates functional fields (StackSize and future additions). Appearance fields (DisplayName, DescriptionText, Icon) always apply when non-null regardless of ChangesEnabled. |
| **Stash** | A server-side logical item store per player, entirely outside ECS. Exists in two forms: PlayerStash (carried) and CastleStash (bound to castle). Managed by LilithsTreasury (planned). |
| **Silent Command** | A `[[LE::...]]` sentinel sent by Soul via `ChatMessageEvent { MessageType = Local }` — intercepted by `ServerChatSystemPatch` on the server. Soul has no VCF dependency; all Soul→Heart communication uses this pattern. |
| **Ritual** | A LilithsBlessings construct — a structured sacrifice recipe that, when completed, applies a buff to a defined scope (player, clan, or server). |
| **Expedition** | A time-based mission assigned to custom units (Conquest) or creatures (Menagerie). Runs as a server-side timer; outcome is simulated by Heart on completion. |
| **MenagerieCreature** | A persistent managed C# data record representing a captured/bred creature in LilithsMenagerie. Not a real ECS entity — stored in JSON. |
| **Infamy** | A per-player, per-faction reputation value tracked by LilithsAdversaries. Increases when the player provokes a faction; decays over real time. |
| **Schematic** | A LilithsArchitects JSON definition describing a complete castle layout as a set of tile/object placements relative to an origin point. |
| **HeartEventBus** | The pub/sub event system in LilithsHeart. All cross-module events are published here. Modules communicate exclusively via this bus — no direct cross-module references. |
| **SoulEventBus** | The Soul-side equivalent of `HeartEventBus`. A pub/sub bus internal to LilithsSoul. Used by Soul client feature areas (Camera, CeilingTiles, Appearances) to communicate with each other and with the Soul UI layer. Never crosses plugin boundaries. Lives in `LilithsSoul/Foundation/SoulEventBus.cs`. |
| **SoulOptionsRegistry** | Soul's bridge to V Rising's native options menu and keybind system. All player-facing controls (camera hotkeys, ceiling tile hotkeys, sliders) register through this class. Not yet implemented — requires RetroCam API research. Lives in `LilithsSoul/Foundation/SoulOptionsRegistry.cs`. |
| **CameraFeature** | Soul-internal feature area (`LilithsSoul/Features/CameraFeature.cs`) that adds third person and first person camera modes to V Rising's default bird's eye view. Gated behind `SoulConfig.CameraEnabled` (default `false`). Initialises only when enabled — zero cost when disabled. |
| **CameraMode** | The active camera perspective. Three values: `BirdsEye` (V Rising default), `ThirdPerson`, `FirstPerson`. The active mode is persisted locally between sessions. Cycled via a single bindable hotkey (forward press / Shift+press for reverse). |
| **CeilingTileFeature** | Soul-internal feature area (`LilithsSoul/Features/CeilingTileFeature.cs`) that renders mirrored structural floor tiles as ceiling tiles for interior immersion. Gated behind `SoulConfig.CeilingTilesDefaultEnabled` (default `false`). Toggled at runtime via a hotkey. Zero cost when disabled. |
| **CeilingTileMode** | The rendering strategy used by `CeilingTileFeature`. `Auto` (default) — tries occlusion-driven first, falls back to radius-driven silently. `OcclusionDriven` — mirrors Unity renderer visibility callbacks. `RadiusDriven` — maintains a tile grid within a configured horizontal radius and vertical layer count around the player. |
| **AppearanceFeature** | Soul-internal feature area (`LilithsSoul/Features/AppearanceFeature.cs`) that owns appearance receiving, caching, whitelist filtering, and texture application. Gated behind `SoulConfig.AppearancesEnabled` (default `false`). Initialises `AppearanceSyncReceiver`, `AppearanceTextureCache`, `AppearanceApplicator`, and `AppearanceWhitelistService` when enabled. |
| **AppearanceSync** | The isolated parallel sync channel for per-player character texture overrides. Completely separate from the standard `SyncPayloadCache`/`SyncSender`/`SyncQueue` pipeline. Active only when `HeartConfig.AppearanceSyncEnabled = true` on the server and `SoulConfig.AppearancesEnabled = true` on the client. Uses the `[[LE::appearance:...]]` sentinel family. |
| **AppearanceStore** | Heart-side class that owns read/write of `Custom/<SteamId>/appearance.json` per player under `LilithsHeart/Appearances/Custom/`. In-memory cache — no per-request disk I/O. Writes on player save, loads all on Heart startup. |
| **AppearanceSyncSender** | Heart-side class that broadcasts appearance data to clients via `[[LE::appearance:...]]` sentinels. Handles connect broadcast, update broadcast, clear broadcast, and cooldown response. Part of Heart's Appearances feature area — initialises only when `AppearanceSyncEnabled = true`. |
| **AppearancePermissionService** | Heart-side class that reads `LilithsHeart/Appearances/Permissions/approved_players.json`. Resolves `CanSetAppearance` and `CanUseCustomUrls` per player. SteamId is canonical — PlayerName is a human-readable convenience field that self-heals on write if the player has renamed. |
| **AppearanceSyncReceiver** | Soul-side class that listens for `[[LE::appearance:...]]` sentinels and maintains an in-memory `SteamId → AppearanceData` map. Part of `AppearanceFeature`. |
| **AppearanceApplicator** | Soul-side class that hooks character entity spawn/despawn to apply and release texture overrides via `Renderer.material.SetTexture()`. Consults `AppearanceWhitelistService` and `CustomAppearancesEnabled` flag before applying. Part of `AppearanceFeature`. |
| **AppearanceTextureCache** | Soul-side class with a disk-backed cache for URL textures (keyed by URL hash under `LilithsSoul/Cache/<ServerIdentity>/AppearanceCache/`) and a memory cache for bundled style textures. Lazy load — textures are fetched only when a character using them enters render range. Part of `AppearanceFeature`. |
| **AppearanceWhitelistService** | Soul-side class that manages the player's personal whitelist of SteamIds whose custom appearances they consent to rendering. Loads/saves `appearance_whitelist.json` from the server-identity cache folder. Resolves by SteamId; enriches PlayerName from broadcast data. Part of `AppearanceFeature`. |
| **StyleId** | A stable string identifier for a bundled appearance texture (e.g. `"RedLips_01"`, `"PaleRose_01"`). Defined in `styles_manifest.json` under `LilithsHeart/Appearances/Styles/`. Known to both Heart and Soul — Heart does not need to sync the manifest since bundled styles ship with Soul. |
| **BundledStyle** | A curated texture authored by the suite creator, shipped inside Soul's install under `LilithsHeart/Appearances/Styles/`. Referenced by `StyleId`. Always available with no external dependency. Contrasts with a custom URL texture provided by a player or server. |
| **AppearancePreset** | A named set of per-slot appearance overrides saved by a player. Stored locally in Soul's cache (unlimited presets). The server stores up to `MaxPresetsPerPlayer` (default 4) presets per player in `AppearanceStore`. Only the active preset's resolved slot state is broadcast to Heart and other clients. |
| **AppearanceSyncMode** | `HeartConfig` enum controlling which players may broadcast appearances. `Permissive` — all players may broadcast; admins revoke individuals. `Whitelist` — no player may broadcast until explicitly approved in `approved_players.json`. |
| **LUI** | LilithUserInterface — LilithsSoul's data-driven UI framework. Provides panels, HUD elements, and the config editor built entirely on Unity's legacy `UnityEngine.UI` runtime API using extracted V Rising textures. Gated behind `SoulConfig.LUIEnabled` (default `true`). |
| **LUIAssets** | Static registry of all loaded V Rising texture sprites used by LUI. Populated at world-ready time by `LUIAssetLoader` from PNG files in `SoulPathIndex.LUIAssetsDir`. No panel or component does its own texture loading — all asset access goes through this registry. |
| **LUIAssetsDir** | `SoulPathIndex.LUIAssetsDir` — the `LUI/` subdirectory alongside the Soul plugin DLL. Contains all extracted V Rising PNG textures used by the framework. |
| **LUILayoutsDir** | `SoulPathIndex.LUILayoutsDir` — directory scanned recursively at startup for `*.layout.json` files. Modules place their panel definitions here. |
| **Layout JSON** | A `*.layout.json` file defining one or more LUI panels using the element vocabulary. Discovered automatically by Soul at startup. Adding a module's layout JSON requires no Soul code changes. |
| **LUI Element** | A typed UI building block in the layout system. Containers: `LilithPanel`, `LilithButtonTray`, `LilithTabBar`, `LilithScrollView`, `LilithGroup`. Leaves: `LilithButton`, `LilithToggle`, `LilithDropdown`, `LilithTextBox`, `LilithLabel`, `LilithImage`, `LilithSlider`, `LilithSeparator`. Every element has a unique `"Name"` identifier within its scope. |
| **LilithPermissions** | Server-side permission configuration file (`lilithpermissions.json`). Defines which Steam IDs belong to which tier (`Admin`, `Moderator`) and which permission tier is required to open each panel. Travels to Soul as part of the standard `ServerSyncPayload` Critical tier. |
| **Permission Tier** | A privilege level assigned to a connected player. Four tiers: `Player` (default), `Moderator`, `Admin`, `Owner`. UI panel visibility is gated by tier client-side; all sentinel commands are verified server-side regardless. |
| **AdminSyncPayload** | A heavyweight payload containing server config file contents, directory structure, and config schemas. Never pushed to regular players. Requested explicitly by an admin opening the config editor panel. Delivered via the standard chunk system. Cached in memory per file with `Modified` timestamps; not persisted to disk. |
| **Config Editor** | A LUI panel that allows admins to browse, view, and edit server-side config JSON files in-game. Gated behind `SoulConfig.ConfigEditorEnabled` (default `false`). Uses a lazy three-ping directory navigation model. Changes are staged locally and saved explicitly — never applied immediately to the live server. |
| **Staged Edits** | The config editor's write model. Edits made in the panel are held locally until the admin explicitly saves. Save transmits only the changed fields (delta) to Heart. Heart writes the file and updates the `Modified` timestamp. A server reload is required to apply changes to the live server. |
| **HUD Edit Mode** | A LUI mode toggled via the HUD settings panel. When active, all HUD elements display drag handles and visibility toggles. When inactive, elements are fixed in place. Element positions and visibility are persisted to `SoulConfig`. |
| **SoulHudRegistry** | Static registry where modules declare custom HUD resource bars at startup via `SoulHudRegistry.RegisterResource(key, label, colour)`. Heart includes current values for registered resources in sync or targeted updates. Bars appear only when the registering module is active. |
| **ConfigEditorEnabled** | `SoulConfig` bool (default `false`). When false, no AdminSyncPayload handling, directory navigation, file caching, or config editor UI is initialized. Flip to `true` on servers where admins want in-game config editing. |

## V Rising / ECS Terminology

| Term | Definition |
|------|------------|
| **ECS** | Entity Component System — Unity's DOTS architecture. V Rising uses this internally. Entities are IDs, Components are data structs, Systems process them. |
| **Entity** | An `Entity` struct — a lightweight ID referencing a collection of components in the ECS world. |
| **Prefab** | A template Entity stored in `PrefabCollectionSystem._PrefabGuidToEntityMap`. Recipes, items, stations, etc. all have prefab entities that define their base state. |
| **PrefabGUID** | `Stunlock.Core.PrefabGUID` — wraps a single `int` (`_Value`) identifying a prefab. The identity key for all item types, recipes, spells, units, and building tiles in V Rising. |
| **GuidHash** | The raw `int` value of a `PrefabGUID`. Can be negative (signed int). Can be used directly as a config key — `PrefabNameResolver` accepts GuidHash integer strings as the third lookup path. |
| **AssetGuid** | `Stunlock.Core.AssetGuid` — a GUID type used as the key in `Localization._LocalizedStrings`. Maps to display name/description strings. |
| **Component** | A struct implementing `IComponentData` attached to entities. Examples: `RecipeData`, `NetworkId`, `User`, `ItemData`. |
| **DynamicBuffer** | `Unity.Entities.DynamicBuffer<T>` — a resizable array buffer component. Examples: `RecipeRequirementBuffer`, `WorkstationRecipesBuffer`. |
| **World** | `Unity.Entities.World` — a container for entities and systems. V Rising has separate server and client worlds. |
| **EntityManager** | `Unity.Entities.EntityManager` — the API for creating, reading, writing, and destroying entities and components. |
| **System** | A class that processes entities. Accessible via `World.GetExistingSystemManaged<T>()`. |
| **GameDataSystem** | V Rising's system holding the `GameDatas` struct, which contains all authoritative lookup maps (`RecipeHashLookupMap`, `ItemHashLookupMap`, etc.). |
| **PrefabCollectionSystem** | V Rising's system holding `_PrefabGuidToEntityMap` (PrefabGUID → Entity). |
| **RecipeHashLookupMap** | A `NativeParallelHashMap<PrefabGUID, RecipeData>` in `GameDataSystem`. The crafting completion system reads scalar fields (CraftDuration, AlwaysUnlocked, etc.) from here — not from entity components. Must be written last in the init sequence (after all `RegisterRecipes()`/`RegisterGameData()` calls) to survive registration resets. |
| **ItemHashLookupMap** | A `NativeParallelHashMap<PrefabGUID, ItemData>` in `GameDataSystem`. The inventory system reads `MaxAmount` (stack size) from here — not from the item prefab entity component. Patched by `ItemFunctionService` alongside the entity write. Same last-write ordering requirement as `RecipeHashLookupMap`. |
| **WorkstationRecipesBuffer** | `DynamicBuffer<WorkstationRecipesBuffer>` — defines which recipes appear at a crafting station or for a player. |
| **ItemData** | `ProjectM.ItemData` — ECS component on item prefab entities. `MaxAmount` field controls stack size. Patched by `ItemFunctionService` on both the prefab entity and `ItemHashLookupMap`. |
| **FakeItem** | A prefab entity that acts as an ephemeral item consumed immediately by a V Rising subsystem rather than delivered to player inventory. Prisoner feed recipes output FakeItems (`FakeItem_FeedPrisoner_*`, `FakeItem_Prisoner_*`) which `UpdatePrisonSystem` reads and discards in the same tick. FakeItems carry behaviour components (`ProjectM.FeedPrisoner`, `ProjectM.AffectPrisonerWithToxic`, `ProjectM.DealDamageToPrisoner`) that define stat effects. They have no live world instances — only prefab entities. Patched by `PrisonerFeedSystem`. |
| **IL2CPP** | Ahead-of-time compilation mode used by V Rising. BepInEx + HarmonyLib patch it via runtime method replacement. |
| **Two-pass Patching** | Pattern used by StationSystem — Pass 1 patches prefab entities, Pass 2 patches live world entities after `RegisterGameData()`. Required because `RegisterGameData()` resets live entity buffers but not prefab entities. |
| **Prefab Tag Retention** | V Rising keeps the `Unity.Entities.Prefab` tag on placed world instances (contrary to standard Unity ECS convention). `None=[Prefab]` query exclusion is therefore ineffective for workstation patching. Solution: `GetAllEntities()` with direct prefab entity identity exclusion. |

## Sync Protocol Terminology

| Term | Definition |
|------|------------|
| **ServerSyncPayload** | The main data contract sent from Heart to Soul on client connect. Contains `ServerLanguage`, appearance overrides, recipe overrides, station changes, player recipe changes, and `LilithPermissions` tier data. |
| **ChunkPush** | Default sync transport. Payload delivered as tiered GZip+Base64 chunks via chat messages. Controlled rate via `ChunksPerFrame` setting. |
| **HttpServer** | Sync transport where Heart hosts an `HttpListener` endpoint. Soul fetches the full payload via HTTP on connect. Requires firewall port open. |
| **StaticUrl** | Sync transport where the admin hosts the payload at a static URL. Heart sends a redirect sentinel; Soul fetches directly. |
| **SyncFallbackToChunks** | HeartConfig bool (default true). When true and an HTTP fetch fails, Soul sends `[[LE::sync-fallback]]` and Heart delivers chunks. When false, a failed fetch gives up. |
| **PayloadHash** | First 8 hex characters of SHA256 hash of the serialized payload. Used by Soul to detect changes and avoid redundant disk writes. |
| **Tier** | A slice of the sync payload sent as an independent unit. Critical (appearance), High (recipes/stations), Normal (player recipes). Each tier applied immediately on receipt. |
| **[[LE::begin/end/chunk]]** | ChunkPush protocol sentinels. `[[LE::begin:T:N:CKSUM]]`, `[[LE::T:NNNN]]<data>`, `[[LE::end:T:CKSUM]]`. |
| **[[LE::sync-url:...]]** | Redirect sentinel from Heart. Format: `[[LE::sync-url:<url>:<fallback>]]`. Soul parses URL + fallback flag, attempts HTTP fetch. |
| **[[LE::sync-fallback]]** | Soul→Heart sentinel. Sent when HTTP fetch fails and fallback enabled. Heart enqueues chunks for that client. |
| **[[LE::lang-request:X]]** | Soul→Heart sentinel. Sent when `PreferredLanguage` differs from `ServerLanguage`. Heart responds with a localization payload or `[[LE::lang-unavailable:X]]`. |
| **[[LE::lang-unavailable:X]]** | Heart→Soul sentinel. Sent when requested language has no configured overrides. Soul logs a warning and stays on default language. |
| **ServerIdentity** | The sanitized server name from `HeartConfig.ServerName`. Used as a folder name on the client for per-server cached data. |
| **ChatMessageEvent** | `ProjectM.Network.ChatMessageEvent` — client-side outgoing chat struct. Used by Soul to send `[[LE::...]]` sentinels to Heart. `MessageType = ChatMessageType.Local`. |
| **[[LE::admin:dir:...]]** | Soul→Heart sentinel. Requests a directory listing from the server config file tree. Format: `[[LE::admin:dir:<path>]]` where `<path>` is `root` or a relative folder path (e.g. `Recipes/Stations`). Heart responds with a directory listing payload including entry names, types, and `Modified` timestamps. |
| **[[LE::admin:file:...]]** | Soul→Heart sentinel. Requests the content and schema for a specific server config file. Format: `[[LE::admin:file:<path>]]` where `<path>` is a relative file path (e.g. `Recipes/Stations/alchemy.json`). Heart responds with file content JSON and config schema JSON. |
| **[[LE::admin:save:...]]** | Soul→Heart sentinel. Submits a staged edit delta for a specific config file. Format: `[[LE::admin:save:<path>]]` with delta payload. Heart validates admin permission, writes the file, and updates the `Modified` timestamp. Changes require a server reload to take effect. |
| **[[LE::appearance:update:...]]** | Soul→Heart sentinel. Player submitting their active appearance preset to the server. Heart validates `AppearanceSyncEnabled`, permission flags, and per-player cooldown before writing and broadcasting. |
| **[[LE::appearance:clear]]** | Soul→Heart sentinel. Player clearing their own appearance. Heart clears `AppearanceStore` entry and broadcasts `[[LE::appearance:clear:<steamid>]]` to all clients. |
| **[[LE::appearance:data:...]]** | Heart→Soul sentinel. Full appearance snapshot for one player. Format: `[[LE::appearance:data:<steamid>:<payload>]]`. Sent to all online clients when a player connects or updates their appearance. |
| **[[LE::appearance:clear:...]]** | Heart→Soul sentinel. Instructs all clients to remove a specific player's appearance overrides immediately. Sent when permission is revoked or a player clears their own appearance. Format: `[[LE::appearance:clear:<steamid>]]`. |
| **[[LE::appearance:cooldown:...]]** | Heart→Soul sentinel. Sent to the requesting client only when an appearance update is rejected due to cooldown. Format: `[[LE::appearance:cooldown:<seconds>]]`. Soul stores the value and suppresses resends until elapsed. |
| **[[LE::appearance:maxweapons:...]]** | Heart→Soul sentinel. Sent on player connect. Communicates the server's `MaxWeaponAppearances` setting so Soul knows how many recent weapon type appearance overrides to include in its next appearance update. |

## Config / Data Terminology

| Term | Definition |
|------|------------|
| **PrefabDef** | A `readonly record struct` in LilithsMind defining a single prefab's metadata (Name, GuidHash, Prefab, NameKey, DescKey). |
| **LilithItemData** | Unified item override DTO. Fields: `DisplayName?`, `DescriptionText?`, `Icon?`, `ChangesEnabled`, `StackSize?`. Appearance fields always apply when non-null. `ChangesEnabled` gates `StackSize`. Server-side only — `StackSize` and `ChangesEnabled` are filtered out of the sync payload. |
| **RecipeEntryData** | Config DTO for recipe overrides. Scalar fields (`CraftDuration`, `AlwaysUnlocked`, etc.) + optional buffer lists (`Requirements`, `Outputs`, `RepairCosts`, `UnitOutputs`, `RecipeLinks`). |
| **PrisonerFeedEntryData** | Config DTO for prisoner FakeItem stat overrides. `Type` (PrisonerFeedTypeEnum) + type-specific fields. Three types: FeedPrisoner, DealDamageToPrisoner, AffectWithToxic. **Known constraint:** setting `AlterBloodQuality_Min`/`Max` to a non-zero value on a `FeedPrisoner` FakeItem requires valid `BuffIncreaseBloodQualitySuccess`/`Fail` GUIDs on the `ProjectM.FeedPrisoner` component. Without them, `UpdatePrisonSystem.UpdatePrison()` fails a `PrefabLookupMap` lookup every frame and the feed action loops infinitely, refunding the item on cancel. Vanilla buff GUIDs: `PrisonerBloodQualityChangeSuccessBuff` (-167264377), `PrisonerBloodQualityChangeFailBuff` (-638893418). **Revisit:** wire valid buff GUIDs into `PatchFeedPrisoner()` when `AlterBloodQuality` is non-zero. Until then, leave `AlterBloodQuality` unset or at `0.0`. |
| **LilithRecipeData** | Network DTO in `ServerSyncPayload.RecipeOverrides`. |
| **LilithStationData** | Network DTO in `ServerSyncPayload.StationRecipeOverrides`. Contains `RecipesToAdd` and `RecipesToRemove`. |
| **LanguageCodeEnum** | Enum in `LilithsMind/Data/` listing all V Rising / Steam language codes plus Custom. Folder names under `Localization/` must match enum member names. |
| **SyncModeEnum** | Enum in `LilithsMind/Data/` — `ChunkPush`, `HttpServer`, `StaticUrl`. |
| **SyncTierEnum** | Enum in `LilithsMind/Data/` — `Critical(0)`, `High(1)`, `Normal(2)`, `Low(3)`, `Background(4)`. |

## Development / Code Organization

| Term | Definition |
|------|------------|
| **`[CHANGED]`** | Inline comment marker documenting changes from previous code iterations. Essential for understanding evolution. |
| **`[PERFORMANCE]`** | Inline comment marker documenting performance characteristics and O-notation of operations. |
| **Harmony Patch** | A `[HarmonyPrefix]` or `[HarmonyPostfix]` method that injects code before or after a game method. Named `*Patch.cs`. |
| **Single-fire Guard** | A `static bool _initialized` field that prevents a Harmony patch from executing more than once. |
| **Apply-layer Service** | A service that only reads from `LilithItemConfig` and logs diagnostics. No file I/O. `LocalizationService` and `InterfaceService` are apply-layer services — all loading is done by `ItemService`. |
| **Embedded Resource** | A JSON file compiled into the DLL via `<EmbeddedResource>` in `.csproj`. Retrieved at runtime via `Assembly.GetManifestResourceStream()`. Used for all example and debug config templates. Resource name format: `<AssemblyName>.Resources.<Subfolder>.<FileName>`. |

## EventKind Range Reservation

| Range | Module |
|-------|--------|
| 0–99 | Core (reserved) |
| 100–199 | LilithsCookbook |
| 200–299 | LilithsBounty |
| 300–399 | LilithsTreasury |
| 400–499 | LilithsMachinations |
| 500–599 | LilithsAdversaries |
| 600–699 | LilithsConquest |
| 700–799 | LilithsMenagerie |
| 800–899 | LilithsBlessings |
| 900–999 | LilithsWisdom |
| 1000–1099 | LilithsNexus |
| 1100–1199 | LilithsGrimoire |
| 1200–1299 | LilithsArchitects |
| 1300–1399 | LilithsMachinations (extended) |

> These ranges are for **Heart-side** (`HeartEventBus`) events only.
> Soul-side (`SoulEventBus`) event ranges are documented in `ARCHITECTURE.md`
> under the SoulEventBus section.