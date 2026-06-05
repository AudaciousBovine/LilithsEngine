# Glossary

## Project Terminology

| Term | Definition |
|------|------------|
| **LilithsGarden** | The overall mod suite name. Thematic naming: Heart (server core), Soul (client core), Mind (shared knowledge), Cookbook (recipe module). |
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
| **PreferredLanguage** | Soul-side setting (`SoulConfig.PreferredLanguage`). If it differs from `ServerLanguage`, Soul sends `[[LG:lang-request:X]]` to Heart after receiving the Critical tier. |
| **LocalizationSyncPayload** | A `ServerSyncPayload` with only `ItemAppearanceOverrides` populated (DisplayName + DescriptionText, no Icon, no StackSize). Sent by `LocalizationSyncSender` when Soul requests a non-default language. Cached to `localization_<language>.json` on the client. |
| **Alias** | An admin-defined short name for a prefab, stored in `Aliases/<IndexClassName>.json`. Overrides the compiled `Name` field from LilithsMind on a per-server basis. Resolved by `PrefabNameResolver` as the first lookup path. |
| **GenerateNameAliasConfigs** | HeartConfig flag that dumps compiled `Name` defaults to `Aliases/*.json` so admins have a starting point for customization. Always overwrites. |
| **ChangesEnabled** | A bool field on `LilithItemData` that gates functional fields (StackSize and future additions). Appearance fields (DisplayName, DescriptionText, Icon) always apply when non-null regardless of ChangesEnabled. |
| **Stash** | A server-side logical item store per player, entirely outside ECS. Exists in two forms: PlayerStash (carried) and CastleStash (bound to castle). Managed by LilithsTreasury (planned). |
| **Silent Command** | A `[[LG:...]]` sentinel sent by Soul via `ChatMessageEvent { MessageType = Local }` — intercepted by `ServerChatSystemPatch` on the server. Soul has no VCF dependency; all Soul→Heart communication uses this pattern. |
| **Ritual** | A LilithsBlessings construct — a structured sacrifice recipe that, when completed, applies a buff to a defined scope (player, clan, or server). |
| **Expedition** | A time-based mission assigned to custom units (Conquest) or creatures (Menagerie). Runs as a server-side timer; outcome is simulated by Heart on completion. |
| **MenagerieCreature** | A persistent managed C# data record representing a captured/bred creature in LilithsMenagerie. Not a real ECS entity — stored in JSON. |
| **Infamy** | A per-player, per-faction reputation value tracked by LilithsAdversaries. Increases when the player provokes a faction; decays over real time. |
| **Schematic** | A LilithsArchitects JSON definition describing a complete castle layout as a set of tile/object placements relative to an origin point. |
| **HeartEventBus** | The pub/sub event system in LilithsHeart. All cross-module events are published here. Modules communicate exclusively via this bus — no direct cross-module references. |

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
| **GameDataSystem** | V Rising's system holding `RecipeHashLookupMap` — a dictionary mapping `PrefabGUID → RecipeData`. |
| **PrefabCollectionSystem** | V Rising's system holding `_PrefabGuidToEntityMap` (PrefabGUID → Entity). |
| **RecipeHashLookupMap** | A `NativeHashMap<PrefabGUID, RecipeData>` in `GameDataSystem`. Crafting reads scalar fields from here, not from entity components. |
| **WorkstationRecipesBuffer** | `DynamicBuffer<WorkstationRecipesBuffer>` — defines which recipes appear at a crafting station or for a player. |
| **ItemData** | `ProjectM.ItemData` — ECS component on item prefab entities. `MaxAmount` field controls stack size. Patched by `ItemFunctionService` for StackSize overrides. |
| **IL2CPP** | Ahead-of-time compilation mode used by V Rising. BepInEx + HarmonyLib patch it via runtime method replacement. |
| **Two-pass Patching** | Pattern used by StationSystem — Pass 1 patches prefab entities, Pass 2 patches live world entities after `RegisterGameData()`. Required because `RegisterGameData()` resets live entity buffers but not prefab entities. |
| **Prefab Tag Retention** | V Rising keeps the `Unity.Entities.Prefab` tag on placed world instances (contrary to standard Unity ECS convention). `None=[Prefab]` query exclusion is therefore ineffective for workstation patching. Solution: `GetAllEntities()` with direct prefab entity identity exclusion. |

## Sync Protocol Terminology

| Term | Definition |
|------|------------|
| **ServerSyncPayload** | The main data contract sent from Heart to Soul on client connect. Contains `ServerLanguage`, appearance overrides, recipe overrides, station changes, and player recipe changes. |
| **ChunkPush** | Default sync transport. Payload delivered as tiered GZip+Base64 chunks via chat messages. Controlled rate via `ChunksPerFrame` setting. |
| **HttpServer** | Sync transport where Heart hosts an `HttpListener` endpoint. Soul fetches the full payload via HTTP on connect. Requires firewall port open. |
| **StaticUrl** | Sync transport where the admin hosts the payload at a static URL. Heart sends a redirect sentinel; Soul fetches directly. |
| **SyncFallbackToChunks** | HeartConfig bool (default true). When true and an HTTP fetch fails, Soul sends `[[LG:sync-fallback]]` and Heart delivers chunks. When false, a failed fetch gives up. |
| **PayloadHash** | First 8 hex characters of SHA256 hash of the serialized payload. Used by Soul to detect changes and avoid redundant disk writes. |
| **Tier** | A slice of the sync payload sent as an independent unit. Critical (appearance), High (recipes/stations), Normal (player recipes). Each tier applied immediately on receipt. |
| **[[LG:begin/end/chunk]]** | ChunkPush protocol sentinels. `[[LG:begin:T:N:CKSUM]]`, `[[LG:T:NNNN]]<data>`, `[[LG:end:T:CKSUM]]`. |
| **[[LG:sync-url:...]]** | Redirect sentinel from Heart. Format: `[[LG:sync-url:<url>:<fallback>]]`. Soul parses URL + fallback flag, attempts HTTP fetch. |
| **[[LG:sync-fallback]]** | Soul→Heart sentinel. Sent when HTTP fetch fails and fallback enabled. Heart enqueues chunks for that client. |
| **[[LG:lang-request:X]]** | Soul→Heart sentinel. Sent when `PreferredLanguage` differs from `ServerLanguage`. Heart responds with a localization payload or `[[LG:lang-unavailable:X]]`. |
| **[[LG:lang-unavailable:X]]** | Heart→Soul sentinel. Sent when requested language has no configured overrides. Soul logs a warning and stays on default language. |
| **ServerIdentity** | The sanitized server name from `HeartConfig.ServerName`. Used as a folder name on the client for per-server cached data. |
| **ChatMessageEvent** | `ProjectM.Network.ChatMessageEvent` — client-side outgoing chat struct. Used by Soul to send `[[LG:...]]` sentinels to Heart. `MessageType = ChatMessageType.Local`. |

## Config / Data Terminology

| Term | Definition |
|------|------------|
| **PrefabDef** | A `readonly record struct` in LilithsMind defining a single prefab's metadata (Name, GuidHash, Prefab, NameKey, DescKey). |
| **LilithItemData** | Unified item override DTO. Fields: `DisplayName?`, `DescriptionText?`, `Icon?`, `ChangesEnabled`, `StackSize?`. Appearance fields always apply when non-null. `ChangesEnabled` gates `StackSize`. Server-side only — `StackSize` and `ChangesEnabled` are filtered out of the sync payload. |
| **RecipeEntryData** | Config DTO for recipe overrides. Scalar fields (`CraftDuration`, `AlwaysUnlocked`, etc.) + optional buffer lists (`Requirements`, `Outputs`, `RepairCosts`, `UnitOutputs`, `RecipeLinks`). |
| **PrisonerFeedEntryData** | Config DTO for prisoner FakeItem stat overrides. `Type` (PrisonerFeedTypeEnum) + type-specific fields. Three types: FeedPrisoner, DealDamageToPrisoner, AffectWithToxic. |
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