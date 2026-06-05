using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Il2CppInterop.Runtime;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using LilithsMind.Data;    // SyncTierEnum, LanguageCodeEnum
using LilithsMind.Network; // ServerSyncPayload
using LilithsSoul.Config;
using LilithsSoul.Foundation;
using LilithsSoul.Services;

// ============================================================
//  SyncReceiver — LilithsSoul
//  LilithsSoul/Network/SyncReceiver.cs
//
//  Intercepts system chat messages from Heart and reassembles
//  the TIERED, chunked, GZip+Base64 ServerSyncPayload.
//
//  Wire protocol (must match LilithsHeart.SyncSender / SyncPayloadCache):
//  ─────────────────────────────────────────────────────────────────────
//    [[LG:begin:T:N:CKSUM]]      begin a tier — T=tier int, N=chunk count,
//                                CKSUM=first 8 hex chars of SHA256
//    [[LG:T:NNNN]]<base64>       chunk NNNN of tier T (zero-padded index);
//                                payload is a slice of the tier's ONE Base64
//                                string (whole gzip blob Base64'd, THEN sliced)
//    [[LG:end:T:CKSUM]]          end tier T — verify + decode + apply
//
//  Redirect sentinel (HttpServer / StaticUrl modes):
//  ──────────────────────────────────────────────────
//    [[LG:sync-url:<url>:<fallback>]]
//      url      — HTTP URL to fetch the full payload from
//      fallback — "1" = request chunk fallback on failure,
//                 "0" = log warning and give up
//
//  Decode pipeline (exact inverse of SyncPayloadCache.BuildBlob):
//  ──────────────────────────────────────────────────────────────
//    1. Concatenate this tier's chunk strings IN INDEX ORDER → the full
//       Base64 string (the encoder Base64'd the whole blob once, then
//       sliced the string — so we concat strings first, decode once).
//    2. Verify: SHA256(UTF8(base64String)) → hex → first 8 chars, compared
//       case-insensitively to the sentinel CKSUM. NOTE the hash is over the
//       Base64 TEXT (matching SyncPayloadCache.ComputeHash(encoded)), not the
//       raw gzip bytes — the TierBlobData doc-comment is slightly misleading;
//       this mirrors the actual encoder code.
//    3. Convert.FromBase64String → gzip bytes → GZipStream decompress → UTF8
//       → JSON → ServerSyncPayload (only THIS tier's slice is populated).
//
//  Per-tier (NOT whole-payload) application:
//  ──────────────────────────────────────────
//  The encoder sends partial payloads per tier — Critical carries only
//  ItemAppearanceOverrides, High only Recipe/Station, Normal only player
//  recipes. So each tier is applied additively the moment its end sentinel
//  verifies — Critical's UI overrides land before High even finishes
//  sending. ApplyTier() switches on the tier to run only that slice's steps.
//
//  Apply steps by tier (FIXED — DO NOT REORDER within a tier):
//  ────────────────────────────────────────────────────────────
//    Critical → 1. LocalizationPatcher.ClearPrevious()
//               2. LocalizationPatcher.Apply()
//               3. DescriptionPatcher.Clear()
//               4. DescriptionPatcher.Build()
//               5. IconPatcher.ClearPrevious()
//               6. IconPatcher.Apply()
//    High     → 7. RecipePatcher.Apply()
//               8. RecipePatcher.ApplyStationRecipes()
//    Normal   → 9. RecipePatcher.ApplyPlayerRecipes()
//
//  The two text paths (names, descriptions) lead the Critical tier so the
//  overrides are in place before the UI reads them. Both repoint at the
//  data layer: LocalizationPatcher.Apply mints a key for ManagedItemData.Name,
//  and DescriptionPatcher.Build mints a key for the Key field of the
//  ManagedItemData.Description struct (writing the whole struct back). The
//  game's own tooltip pipeline then resolves the minted keys — there is NO
//  UI/hover patch. Clear-then-(re)build pairs keep re-applies idempotent
//  across the pre-apply-from-disk + live double-apply.
//
//  Disk cache (merge accumulator):
//  ────────────────────────────────
//  Tiers carry a shared PayloadHash. As each tier verifies, its slice is
//  merged into a single ServerSyncPayload keyed by that hash and the merged
//  result is written to disk. On reconnect, the whole merged payload is
//  pre-applied from disk before the UI builds (NotifyWorldReady), closing
//  the CharacterHUD-builds-before-payload race.
//
//  World-ready deferral:
//  ──────────────────────
//  If a tier verifies before the client ECS world is ready, its decoded
//  payload is queued and applied in NotifyWorldReady once maps are built.
//
//  [CHANGED] HandleRedirect() added — handles [[LG:sync-url:...]] sentinels
//            sent by Heart in HttpServer and StaticUrl modes. Triggers
//            SyncHttpFetcher to fetch the payload directly. On failure,
//            sends [[LG:sync-fallback]] as a plain chat message to the
//            server (no VCF dependency) — Heart's ClientSyncFallbackPatch
//            intercepts it and enqueues chunk delivery for this client.
//
//  [CHANGED] Rewritten from the stale FLAT protocol ([[LG:N]] / [[LG:end]],
//            single concat→JSON) to this TIERED protocol. The flat receiver
//            could not parse [[LG:begin:T:N:CKSUM]] / [[LG:end:T:CKSUM]], so
//            every chunk was silently unrecognized and no payload ever
//            applied — names, icons, AND descriptions all went dark. This
//            version matches the sender Heart has been emitting.
//
//  [CHANGED] Item DESCRIPTIONS are handled by DescriptionPatcher at the DATA
//            LAYER (folded into the Critical apply, steps 3–4) — the same
//            mint-and-repoint mechanism as names. Descriptions DO persist
//            through managed data: ManagedItemData.Description is a value-type
//            struct whose Key field is a LocalizationKey; repointing it (with
//            a whole-struct write-back) sticks. This replaced an earlier
//            attempt to override descriptions by Harmony-patching the client
//            tooltip-build pipeline — every such target either crashed when
//            patched or never fired on hover (see DATA_FLOW "Why the
//            description override is data-layer"). There is no ItemDescriptionPatch.
//
//  [CHANGED] LanguageCodeEnum.System support added — when PreferredLanguage
//            is System, SystemLanguageResolver.Resolve() reads
//            Localization.CurrentLanguage from the running V Rising client
//            and maps it to a concrete language name before any comparison
//            or lang-request sentinel is sent. System is never sent on the wire.
//
//  [PERFORMANCE] Per-message: a few StartsWith checks on the hot chat path
//                — negligible outside connect. Decode (GZip + Base64 + JSON)
//                and SHA256 run once per tier per connect. Disk I/O once
//                per tier merge. No per-frame cost.
// ============================================================

namespace LilithsSoul.Network;

public static class SyncReceiver
{
    private const string LOG_SOURCE = "LilithsSoul.SyncReceiver";

    private const string BEGIN_PREFIX        = "[[LG:begin:";
    private const string END_PREFIX          = "[[LG:end:";
    private const string CHUNK_PREFIX        = "[[LG:";
    // [CHANGED] Redirect sentinel prefix for HttpServer / StaticUrl modes.
    private const string REDIRECT_PREFIX     = "[[LG:sync-url:";
    // [CHANGED] Language sentinels for multi-language localization.
    private const string LANG_UNAVAILABLE_PREFIX = "[[LG:lang-unavailable:";

    // Per-tier in-flight reassembly state, keyed by tier int.
    sealed class TierAccumulator
    {
        public int             ExpectedChunks;
        public string          Checksum = string.Empty;
        public List<string>    Chunks   = [];
    }

    static readonly Dictionary<int, TierAccumulator> _inFlight = new();

    // Merge accumulator for the disk cache — one payload assembled across
    // tiers, keyed by the shared PayloadHash. Reset when a new hash appears.
    static ServerSyncPayload? _mergeAccumulator;
    static string             _mergeHash = string.Empty;

    static bool                    _clientWorldReady;
    static readonly List<ServerSyncPayload> _pendingTierPayloads = [];
    static string                  _connectionString = string.Empty;

    static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    // ── Called from ClientChatSystemPatch ────────────────────

    /// <summary>
    /// Inspects an incoming system message. If it is a LilithsEngine
    /// sentinel/chunk, handles it and returns true (consumed).
    /// Returns false for unrelated messages.
    /// </summary>
    public static bool TryHandleMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        if (!message.StartsWith(CHUNK_PREFIX, StringComparison.Ordinal)) return false;

        try
        {
            // [CHANGED] Handle redirect sentinel BEFORE the generic chunk/begin/end
            //           dispatch — redirect starts with [[LG:sync-url: which would
            //           otherwise fall through to HandleChunk incorrectly.
            if (message.StartsWith(REDIRECT_PREFIX, StringComparison.Ordinal))
                HandleRedirect(message);
            // [CHANGED] Language unavailable notification from Heart.
            else if (message.StartsWith(LANG_UNAVAILABLE_PREFIX, StringComparison.Ordinal))
                HandleLangUnavailable(message);
            else if (message.StartsWith(BEGIN_PREFIX, StringComparison.Ordinal))
                HandleBegin(message);
            else if (message.StartsWith(END_PREFIX, StringComparison.Ordinal))
                HandleEnd(message);
            else
                HandleChunk(message);
        }
        catch (Exception ex)
        {
            SoulLogger.Error(LOG_SOURCE, $"Failed handling sync message: {ex.Message}");
        }

        // Any [[LG: message is ours — consume it so it never hits chat UI.
        return true;
    }

    /// <summary>
    /// Called by ClientInitPatch when the client ECS world is ready.
    /// Builds all lookup tables, pre-applies cached sync, and applies
    /// any tier payloads that arrived before the world was ready.
    /// </summary>
    public static void NotifyWorldReady(string connectionString)
    {
        _clientWorldReady = true;
        _connectionString = connectionString;

        // Build all lookup tables now that game data is available.
        LocalizationPatcher.BuildNameMap();
        DescriptionPatcher.BuildMap();
        RecipePatcher.BuildNameMap();
        IconPatcher.BuildSpriteMaps();

        TryPreApplyCachedSync(connectionString);

        // [CHANGED] Pre-apply cached localization payload if available.
        TryPreApplyCachedLocalization(connectionString);

        if (_pendingTierPayloads.Count > 0)
        {
            SoulLogger.Info(LOG_SOURCE,
                $"Client world now ready — applying {_pendingTierPayloads.Count} " +
                "queued tier payload(s).");

            foreach (var p in _pendingTierPayloads)
                ApplyTier(p);

            _pendingTierPayloads.Clear();
        }
    }

    // ── Sentinel handlers ─────────────────────────────────────

    /// <summary>
    /// Handles [[LG:sync-url:<url>:<fallback>]] redirect sentinel.
    /// Triggers SyncHttpFetcher to fetch the payload from the given URL.
    /// On success: applies and caches the payload.
    /// On failure: requests chunk fallback via VCF command if fallback=1,
    ///             otherwise logs a warning and gives up.
    ///
    /// [CHANGED] Added for HttpServer and StaticUrl sync modes.
    /// URL may contain colons (https://) so the fallback flag is split
    /// from the END of the inner string, not position-based.
    /// </summary>
    static void HandleRedirect(string message)
    {
        // Strip REDIRECT_PREFIX and trailing "]]"
        var inner = message[REDIRECT_PREFIX.Length..^2];

        // URL may contain colons — split from the END to isolate the fallback flag.
        var lastColon = inner.LastIndexOf(':');
        if (lastColon < 0)
        {
            SoulLogger.Warning(LOG_SOURCE,
                $"Malformed redirect sentinel (missing fallback flag): '{message}'");
            return;
        }

        var url          = inner[..lastColon];
        var fallbackFlag = inner[(lastColon + 1)..];
        bool fallback    = fallbackFlag == "1";

        if (string.IsNullOrWhiteSpace(url))
        {
            SoulLogger.Warning(LOG_SOURCE, "Received empty sync-url redirect — ignoring.");
            return;
        }

        SoulLogger.Info(LOG_SOURCE,
            $"Received sync redirect → '{url}' (fallback={fallback}). Fetching...");

        SyncHttpFetcher.Fetch(url,
            onSuccess: payload =>
            {
                // Register server connection → identity mapping.
                if (!string.IsNullOrEmpty(_connectionString) &&
                    !string.IsNullOrEmpty(payload.ServerIdentity))
                    ServerRegistry.Register(_connectionString, payload.ServerIdentity);

                MergeAndCache(payload);

                if (_clientWorldReady)
                    ApplyTier(payload);
                else
                    _pendingTierPayloads.Add(payload);

                SoulLogger.Info(LOG_SOURCE, "HTTP sync fetch applied successfully.");
            },
            onFailure: () =>
            {
                SoulLogger.Warning(LOG_SOURCE, "HTTP sync fetch failed.");

                if (fallback)
                {
                    SoulLogger.Info(LOG_SOURCE,
                        "Fallback enabled — sending [[LG:sync-fallback]] to server.");
                    SendFallbackSentinel();
                }
                else
                {
                    SoulLogger.Warning(LOG_SOURCE,
                        "Fallback disabled — sync not applied. " +
                        "Reconnect or ask server admin to switch to ChunkPush mode.");
                }
            });
    }

    static void HandleBegin(string message)
    {
        // [[LG:begin:T:N:CKSUM]]
        var body = Unwrap(message);                 // begin:T:N:CKSUM
        var parts = body.Split(':');                // [begin, T, N, CKSUM]
        if (parts.Length != 4)
        {
            SoulLogger.Warning(LOG_SOURCE, $"Malformed begin sentinel: '{message}'");
            return;
        }

        int tier = int.Parse(parts[1]);
        var acc = new TierAccumulator
        {
            ExpectedChunks = int.Parse(parts[2]),
            Checksum       = parts[3],
            Chunks         = [],
        };
        _inFlight[tier] = acc;
    }

    static void HandleChunk(string message)
    {
        // [[LG:T:NNNN]]<base64>
        int close = message.IndexOf("]]", CHUNK_PREFIX.Length, StringComparison.Ordinal);
        if (close < 0) return;

        var header = message[CHUNK_PREFIX.Length..close];   // T:NNNN
        var data   = message[(close + 2)..];

        var parts = header.Split(':');                       // [T, NNNN]
        if (parts.Length != 2) return;

        int tier = int.Parse(parts[0]);
        if (!_inFlight.TryGetValue(tier, out var acc))
        {
            // Chunk arrived before begin — start a tolerant accumulator.
            acc = new TierAccumulator();
            _inFlight[tier] = acc;
        }

        acc.Chunks.Add(data);
    }

    static void HandleEnd(string message)
    {
        // [[LG:end:T:CKSUM]]
        var body  = Unwrap(message);                // end:T:CKSUM
        var parts = body.Split(':');                // [end, T, CKSUM]
        if (parts.Length != 3)
        {
            SoulLogger.Warning(LOG_SOURCE, $"Malformed end sentinel: '{message}'");
            return;
        }

        int tier   = int.Parse(parts[1]);
        var cksum  = parts[2];

        if (!_inFlight.TryGetValue(tier, out var acc))
        {
            SoulLogger.Warning(LOG_SOURCE,
                $"End sentinel for tier {tier} with no in-flight data — ignoring.");
            return;
        }

        _inFlight.Remove(tier);

        // 1. Reassemble the full Base64 string (concat chunks in arrival order;
        //    Heart sends them sequentially and SchedulerPatch preserves order).
        var base64 = string.Concat(acc.Chunks);

        // 2. Verify checksum over the Base64 TEXT (matches encoder).
        var actual = ComputeHash(base64);
        if (!string.Equals(actual, cksum, StringComparison.OrdinalIgnoreCase))
        {
            SoulLogger.Error(LOG_SOURCE,
                $"Tier {tier} checksum mismatch (expected {cksum}, got {actual}) " +
                $"over {acc.Chunks.Count} chunk(s) — discarding tier.");
            return;
        }

        // 3. Base64 → gzip bytes → decompress → JSON.
        ServerSyncPayload? payload;
        try
        {
            var compressed = Convert.FromBase64String(base64);
            var json       = GzipDecompressToString(compressed);
            payload        = JsonSerializer.Deserialize<ServerSyncPayload>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            SoulLogger.Error(LOG_SOURCE, $"Tier {tier} decode failed: {ex.Message}");
            return;
        }

        if (payload == null)
        {
            SoulLogger.Warning(LOG_SOURCE, $"Tier {tier} deserialized to null — ignoring.");
            return;
        }

        SoulLogger.Info(LOG_SOURCE,
            $"Tier {tier} received from '{payload.ServerIdentity}' " +
            $"(hash: {payload.PayloadHash}, " +
            $"appearances: {payload.ItemAppearanceOverrides.Count}, " +
            $"recipes: {payload.RecipeOverrides.Count}, " +
            $"player +{payload.PlayerRecipesToAdd.Count}/-{payload.PlayerRecipesToRemove.Count}).");

        // Register the server connection → identity mapping on first tier.
        if (!string.IsNullOrEmpty(_connectionString) &&
            !string.IsNullOrEmpty(payload.ServerIdentity))
            ServerRegistry.Register(_connectionString, payload.ServerIdentity);

        // [CHANGED] If this is the Critical tier and the server language differs
        //           from our preferred language, request the localization payload.
        //           If PreferredLanguage is System, resolve the actual game language
        //           via SystemLanguageResolver before comparing — System is never
        //           sent on the wire.
        if (tier == (int)SyncTierEnum.Critical &&
            !string.IsNullOrEmpty(payload.ServerLanguage))
        {
            // [CHANGED] Resolve System sentinel to the game's active language.
            //           For any explicit language setting this is a no-op ToString().
            var preferredLang = SoulConfig.PreferredLanguage == LanguageCodeEnum.System
                ? SystemLanguageResolver.Resolve()
                : SoulConfig.PreferredLanguage.ToString();

            if (!string.Equals(preferredLang, payload.ServerLanguage, StringComparison.OrdinalIgnoreCase))
            {
                SoulLogger.Info(LOG_SOURCE,
                    $"Server language '{payload.ServerLanguage}' differs from preferred " +
                    $"'{preferredLang}' — requesting localization payload.");
                RequestLanguage(preferredLang);
            }
        }

        // Merge this tier's slice into the disk cache accumulator + write.
        // [CHANGED] If this is a localization payload (ServerLanguage set and
        //           matches preferred), cache to its own file and apply without
        //           merging into the main sync.json accumulator.
        //           Resolve System sentinel here too — must use the same concrete
        //           language name as the lang-request sent above.
        var preferred = SoulConfig.PreferredLanguage == LanguageCodeEnum.System
            ? SystemLanguageResolver.Resolve()
            : SoulConfig.PreferredLanguage.ToString();

        bool isLocalizationPayload =
            !string.IsNullOrEmpty(payload.ServerLanguage) &&
            !string.Equals(payload.ServerLanguage, "English", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(payload.ServerLanguage, preferred, StringComparison.OrdinalIgnoreCase) &&
            payload.RecipeOverrides.Count == 0 &&
            payload.StationRecipeOverrides.Count == 0;

        if (isLocalizationPayload)
        {
            WriteLocalizationToDisk(payload);
        }
        else
        {
            MergeAndCache(payload);
        }

        // Apply now if the world is ready; otherwise defer to NotifyWorldReady.
        if (_clientWorldReady)
            ApplyTier(payload);
        else
            _pendingTierPayloads.Add(payload);
    }

    // ── Apply ─────────────────────────────────────────────────

    /// <summary>
    /// Applies one tier's slice. Only the steps relevant to the populated
    /// collections run — the tier is inferred from which collections carry
    /// data (the encoder only fills one slice per tier).
    /// </summary>
    static void ApplyTier(ServerSyncPayload payload)
    {
        // Critical slice — item appearance (names, descriptions, icons).
        if (payload.ItemAppearanceOverrides.Count > 0)
        {
            // 1–2. Names — repoint ManagedItemData.Name via LocalizationPatcher.
            LocalizationPatcher.ClearPrevious();
            LocalizationPatcher.Apply(payload);

            // 3–4. Descriptions — repoint the Key of the ManagedItemData.Description
            //      struct via DescriptionPatcher (mint + inject + struct write-back).
            //      Applied here at the data layer, NOT at hover; the game's tooltip
            //      pipeline resolves the minted key on its own.
            DescriptionPatcher.Clear();
            DescriptionPatcher.Build(payload);

            // 5–6. Icons — sprites into ManagedItemData.Icon.
            IconPatcher.ClearPrevious();
            IconPatcher.Apply(payload);
        }

        // High slice — recipes + stations.
        if (payload.RecipeOverrides.Count > 0 || payload.StationRecipeOverrides.Count > 0)
        {
            RecipePatcher.Apply(payload.RecipeOverrides);
            RecipePatcher.ApplyStationRecipes(payload.StationRecipeOverrides);
        }

        // Normal slice — player recipe add/remove.
        if (payload.PlayerRecipesToAdd.Count > 0 || payload.PlayerRecipesToRemove.Count > 0)
        {
            RecipePatcher.ApplyPlayerRecipes(
                payload.PlayerRecipesToAdd,
                payload.PlayerRecipesToRemove);
        }
    }

    // ── Disk cache (merge accumulator) ────────────────────────

    /// <summary>
    /// Merges a tier slice into the per-hash accumulator and writes the
    /// merged payload to disk. A new PayloadHash starts a fresh accumulator
    /// so a server config change doesn't blend old + new data.
    /// </summary>
    static void MergeAndCache(ServerSyncPayload tierPayload)
    {
        // New hash (or first tier) → start a fresh merge accumulator.
        if (_mergeAccumulator == null || _mergeHash != tierPayload.PayloadHash)
        {
            _mergeAccumulator = new ServerSyncPayload
            {
                ServerIdentity = tierPayload.ServerIdentity,
                PayloadHash    = tierPayload.PayloadHash,
            };
            _mergeHash = tierPayload.PayloadHash;
        }

        // Fold this tier's populated collections into the accumulator.
        foreach (var (k, v) in tierPayload.ItemAppearanceOverrides)
            _mergeAccumulator.ItemAppearanceOverrides[k] = v;
        foreach (var (k, v) in tierPayload.RecipeOverrides)
            _mergeAccumulator.RecipeOverrides[k] = v;
        foreach (var (k, v) in tierPayload.StationRecipeOverrides)
            _mergeAccumulator.StationRecipeOverrides[k] = v;

        if (tierPayload.PlayerRecipesToAdd.Count > 0)
            _mergeAccumulator.PlayerRecipesToAdd = new List<string>(tierPayload.PlayerRecipesToAdd);
        if (tierPayload.PlayerRecipesToRemove.Count > 0)
            _mergeAccumulator.PlayerRecipesToRemove = new List<string>(tierPayload.PlayerRecipesToRemove);

        WriteToDisk(_mergeAccumulator);
    }

    static void WriteToDisk(ServerSyncPayload payload)
    {
        try
        {
            Directory.CreateDirectory(SoulPathIndex.ServerDir(payload.ServerIdentity));
            var syncFile = SoulPathIndex.SyncFile(payload.ServerIdentity);
            File.WriteAllText(syncFile,
                JsonSerializer.Serialize(payload,
                    new JsonSerializerOptions { WriteIndented = true }));
            SoulLogger.Debug(LOG_SOURCE, $"Merged sync payload cached to '{syncFile}'.");
        }
        catch (Exception ex)
        {
            SoulLogger.Warning(LOG_SOURCE,
                $"Failed to write merged sync payload to disk: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes a localization payload to its own per-language cache file.
    /// Called when a localization payload is received from Heart.
    /// [CHANGED] Added for multi-language localization caching.
    /// </summary>
    static void WriteLocalizationToDisk(ServerSyncPayload payload)
    {
        if (string.IsNullOrEmpty(payload.ServerIdentity) ||
            string.IsNullOrEmpty(payload.ServerLanguage)) return;

        try
        {
            Directory.CreateDirectory(SoulPathIndex.ServerDir(payload.ServerIdentity));
            var localFile = SoulPathIndex.LocalizationFile(
                payload.ServerIdentity, payload.ServerLanguage);
            File.WriteAllText(localFile,
                JsonSerializer.Serialize(payload,
                    new JsonSerializerOptions { WriteIndented = true }));
            SoulLogger.Debug(LOG_SOURCE,
                $"Localization payload cached to '{localFile}'.");
        }
        catch (Exception ex)
        {
            SoulLogger.Warning(LOG_SOURCE,
                $"Failed to write localization payload to disk: {ex.Message}");
        }
    }

    // ── Pre-apply from disk ───────────────────────────────────

    static void TryPreApplyCachedSync(string connectionString)
    {
        ServerRegistry.Load();

        if (string.IsNullOrEmpty(connectionString))
        {
            SoulLogger.Debug(LOG_SOURCE, "No connection string — cannot pre-apply cached sync.");
            return;
        }

        if (!ServerRegistry.TryGetFolderName(connectionString, out var folderName))
        {
            SoulLogger.Info(LOG_SOURCE,
                $"No cached sync for '{connectionString}' — waiting for server payload.");
            return;
        }

        var syncFile = SoulPathIndex.SyncFile(folderName);
        if (!File.Exists(syncFile))
        {
            SoulLogger.Info(LOG_SOURCE,
                $"Sync file not found for '{folderName}' — waiting for server payload.");
            return;
        }

        try
        {
            var json    = File.ReadAllText(syncFile);
            var payload = JsonSerializer.Deserialize<ServerSyncPayload>(json, _jsonOptions);

            if (payload == null)
            {
                SoulLogger.Warning(LOG_SOURCE,
                    $"Cached sync.json for '{folderName}' deserialized to null.");
                return;
            }

            SoulLogger.Info(LOG_SOURCE,
                $"Pre-applying cached sync for '{folderName}' " +
                $"(hash: {payload.PayloadHash}) before UI builds.");

            // Seed the merge accumulator so live tiers extend the cached set.
            _mergeAccumulator = payload;
            _mergeHash        = payload.PayloadHash;

            ApplyTier(payload);   // cached payload has all slices populated
        }
        catch (Exception ex)
        {
            SoulLogger.Warning(LOG_SOURCE,
                $"Failed to pre-apply cached sync for '{folderName}': {ex.Message}");
        }
    }

    // ── Language sentinels ───────────────────────────────────

    /// <summary>
    /// Sends [[LG:lang-request:<language>]] to Heart requesting
    /// a localization payload for the preferred language.
    /// </summary>
    static void RequestLanguage(string languageName)
    {
        var world = Soul.ClientWorld;
        if (world == null) return;

        try
        {
            var em     = world.EntityManager;
            var entity = em.CreateEntity(
                ComponentType.ReadOnly(Il2CppType.Of<ChatMessageEvent>()));

            em.SetComponentData(entity, new ChatMessageEvent
            {
                MessageText = new FixedString512Bytes($"[[LG:lang-request:{languageName}]]"),
                MessageType = ChatMessageType.Local,
            });

            SoulLogger.Debug(LOG_SOURCE,
                $"Sent [[LG:lang-request:{languageName}]] to server.");
        }
        catch (Exception ex)
        {
            SoulLogger.Warning(LOG_SOURCE,
                $"Failed to send lang-request: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles [[LG:lang-unavailable:<language>]] from Heart.
    /// Logs a warning and stays on the server default language.
    /// </summary>
    static void HandleLangUnavailable(string message)
    {
        var languageName = message[LANG_UNAVAILABLE_PREFIX.Length..^2];
        SoulLogger.Warning(LOG_SOURCE,
            $"Server does not have language '{languageName}' configured. " +
            "Staying on server default language.");
    }

    /// <summary>
    /// Pre-applies the cached localization payload for the preferred language
    /// if available on disk. Called from NotifyWorldReady after sync.json.
    /// [CHANGED] Resolves LanguageCodeEnum.System via SystemLanguageResolver
    ///           so the correct per-language cache file is loaded on reconnect.
    /// </summary>
    static void TryPreApplyCachedLocalization(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return;

        if (!ServerRegistry.TryGetFolderName(connectionString, out var folderName)) return;

        // [CHANGED] Resolve System sentinel — must use the concrete language name
        //           to construct the correct cache file path.
        var preferred = SoulConfig.PreferredLanguage == LanguageCodeEnum.System
            ? SystemLanguageResolver.Resolve()
            : SoulConfig.PreferredLanguage.ToString();

        var localFile  = SoulPathIndex.LocalizationFile(folderName, preferred);

        if (!File.Exists(localFile)) return;

        try
        {
            var json    = File.ReadAllText(localFile);
            var payload = JsonSerializer.Deserialize<ServerSyncPayload>(json, _jsonOptions);

            if (payload == null) return;

            SoulLogger.Info(LOG_SOURCE,
                $"Pre-applying cached localization for '{preferred}' " +
                $"({payload.ItemAppearanceOverrides.Count} override(s)).");

            ApplyTier(payload);
        }
        catch (Exception ex)
        {
            SoulLogger.Warning(LOG_SOURCE,
                $"Failed to pre-apply cached localization for '{preferred}': {ex.Message}");
        }
    }

    // ── Fallback sentinel ────────────────────────────────────

    /// <summary>
    /// Sends [[LG:sync-fallback]] to the server as a plain chat message.
    /// Heart's ClientSyncFallbackPatch intercepts this and enqueues chunk
    /// delivery for this client specifically.
    ///
    /// Uses ChatMessageEvent (ProjectM.Network) in the client ECS world — the same
    /// mechanism the game uses for all player chat messages. No VCF
    /// dependency required on the Soul side.
    ///
    /// [PERFORMANCE] Creates one ECS entity — negligible.
    /// </summary>
    static void SendFallbackSentinel()
    {
        var world = Soul.ClientWorld;
        if (world == null)
        {
            SoulLogger.Warning(LOG_SOURCE,
                "Cannot send fallback sentinel — client world not ready.");
            return;
        }

        try
        {
            var em = world.EntityManager;

            // Creating this entity causes the game to send the message to the server,
            // where Heart's ClientSyncFallbackPatch will intercept and consume it.
            //           The correct outgoing chat type is ChatMessageEvent
            //           (ProjectM.Network) with ChatMessageType.Local.
            var entity = em.CreateEntity(
                ComponentType.ReadOnly(Il2CppType.Of<ChatMessageEvent>()));

            em.SetComponentData(entity, new ChatMessageEvent
            {
                MessageText = new FixedString512Bytes("[[LG:sync-fallback]]"),
                MessageType = ChatMessageType.Local,
            });

            SoulLogger.Debug(LOG_SOURCE, "Sent [[LG:sync-fallback]] to server.");
        }
        catch (Exception ex)
        {
            SoulLogger.Warning(LOG_SOURCE,
                $"Failed to send fallback sentinel: {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────

    /// <summary>Strips the leading "[[LG:" and trailing "]]" from a sentinel.</summary>
    static string Unwrap(string message)
    {
        var inner = message[CHUNK_PREFIX.Length..];
        int end   = inner.IndexOf("]]", StringComparison.Ordinal);
        return end >= 0 ? inner[..end] : inner;
    }

    static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..8];
    }

    static string GzipDecompressToString(byte[] compressed)
    {
        using var input  = new MemoryStream(compressed);
        using var gz     = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gz.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}