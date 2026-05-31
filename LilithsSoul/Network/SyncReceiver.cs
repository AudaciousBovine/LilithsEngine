using System.Text.Json;
using LilithsSoul.Config;
using LilithsSoul.Foundation;
using LilithsSoul.Services;
using LilithsMind.Network;

// ============================================================
//  SyncReceiver — LilithsSoul
//
//  Intercepts system chat messages from Heart and reassembles
//  the chunked ServerSyncPayload.
//
//  Chunk protocol:
//  ───────────────
//  Heart sends N messages of the form [[LG:N]]<content>,
//  followed by a final [[LG:end]] sentinel.
//  SyncReceiver accumulates content strings in order and
//  processes the full JSON when end is received.
//
//  Integration point:
//  ──────────────────
//  ClientChatSystemPatch calls TryHandleMessage(string) for
//  every incoming system message. Returns true if the message
//  was a LilithsGarden chunk (consumed), false otherwise.
//
//  [CHANGED] Display-name localization moved from LocalizationInjector
//            (retired) to LocalizationPatcher. The injector overwrote
//            the string at an item's existing key and reloaded the
//            whole localization table via LoadDefaultLanguage() on
//            clear — which WIPED minted keys and reverted renames to
//            raw GUIDs. LocalizationPatcher instead repoints each item
//            at a freshly minted key (no shared-key contamination, no
//            table reload) and follows the same clear-before-apply
//            lifecycle as IconPatcher.
//
//            TOOLTIPS are not yet handled — ManagedItemData.Description
//            cannot be persisted via managed data (reference property,
//            copy-returning getter). A Harmony patch on the tooltip
//            build path is a separate, pending task. Until it lands,
//            Tooltip overrides are no-ops.
//
//  [CHANGED] NotifyWorldReady builds LocalizationPatcher's name map
//            (was LocalizationInjector.BuildLookupTable()) alongside
//            RecipePatcher.BuildNameMap() and IconPatcher.BuildSpriteMaps().
//
//  Client Payload Application Order (FIXED — DO NOT REORDER):
//  ────────────────────────────────────────────────────────────
//  1. LocalizationPatcher.ClearPrevious()   — restore prior repointed names
//  2. LocalizationPatcher.Apply()           — repoint names (mint + inject + point)
//  3. IconPatcher.ClearPrevious()           — restore original icons
//  4. IconPatcher.Apply()                   — sprites into ManagedItemData
//  5. RecipePatcher.Apply()                 — recipe ECS data
//  6. RecipePatcher.ApplyStationRecipes()   — station buffer patches
//  7. RecipePatcher.ApplyPlayerRecipes()    — player buffer patches
//
//  Localization stays first: name lookups must resolve before the
//  rest of the UI reads them. The clear-then-apply pairs (steps 1–2
//  and 3–4) make re-applies idempotent across the pre-apply +
//  server-payload double-apply.
//
//  [PERFORMANCE] Per-message check is a string.StartsWith on the
//                hot chat path — effectively free outside connect.
//                Deserialization and disk I/O run once per connect.
// ============================================================

namespace LilithsSoul.Network;

public static class SyncReceiver
{
    private const string LOG_SOURCE   = "LilithsSoul.SyncReceiver";
    private const string CHUNK_PREFIX = "[[LG:";
    private const string CHUNK_END    = "[[LG:end]]";

    static readonly List<string> _chunks = [];

    static bool _clientWorldReady;
    static ServerSyncPayload? _pendingPayload;
    static string _connectionString = string.Empty;

    // ── Called from ClientChatSystemPatch ────────────────────

    /// <summary>
    /// Inspects an incoming system message. If it is a LilithsGarden
    /// chunk, accumulates it and returns true (consumed).
    /// Returns false for unrelated messages.
    /// </summary>
    public static bool TryHandleMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;

        if (message == CHUNK_END)
        {
            ProcessAccumulatedChunks();
            return true;
        }

        if (message.StartsWith(CHUNK_PREFIX))
        {
            int closeBracket = message.IndexOf("]]", CHUNK_PREFIX.Length,
                StringComparison.Ordinal);

            if (closeBracket >= 0)
            {
                _chunks.Add(message[(closeBracket + 2)..]);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Called by ClientInitPatch when the client ECS world is ready.
    /// Builds all lookup tables, pre-applies cached sync, and applies
    /// any payload that arrived before world was ready.
    ///
    /// [CHANGED] Builds LocalizationPatcher's name map (was
    ///           LocalizationInjector.BuildLookupTable()).
    /// </summary>
    public static void NotifyWorldReady(string connectionString)
    {
        _clientWorldReady = true;
        _connectionString = connectionString;

        // Build all lookup tables now that game data is available.
        // [CHANGED] LocalizationPatcher.BuildNameMap replaces the retired
        //           LocalizationInjector.BuildLookupTable.
        LocalizationPatcher.BuildNameMap();
        RecipePatcher.BuildNameMap();

        // Build sprite maps for IconPatcher.
        // Resources.FindObjectsOfTypeAll<Sprite>() requires world ready.
        IconPatcher.BuildSpriteMaps();

        TryPreApplyCachedSync(connectionString);

        if (_pendingPayload != null)
        {
            SoulLogger.Info(LOG_SOURCE,
                "Client world now ready — applying queued sync payload.");
            ApplyPayload(_pendingPayload);
            _pendingPayload = null;
        }
    }

    // ── Internal ─────────────────────────────────────────────

    static void TryPreApplyCachedSync(string connectionString)
    {
        ServerRegistry.Load();

        if (string.IsNullOrEmpty(connectionString))
        {
            SoulLogger.Debug(LOG_SOURCE,
                "No connection string — cannot pre-apply cached sync.");
            return;
        }

        if (!ServerRegistry.TryGetFolderName(connectionString, out var folderName))
        {
            SoulLogger.Info(LOG_SOURCE,
                $"No cached sync for '{connectionString}' — " +
                "waiting for server payload.");
            return;
        }

        var syncFile = SoulPathIndex.SyncFile(folderName);
        if (!File.Exists(syncFile))
        {
            SoulLogger.Info(LOG_SOURCE,
                $"Sync file not found for '{folderName}' — " +
                "waiting for server payload.");
            return;
        }

        try
        {
            var json    = File.ReadAllText(syncFile);
            var payload = JsonSerializer.Deserialize<ServerSyncPayload>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload == null)
            {
                SoulLogger.Warning(LOG_SOURCE,
                    $"Cached sync.json for '{folderName}' deserialized to null.");
                return;
            }

            SoulLogger.Info(LOG_SOURCE,
                $"Pre-applying cached sync for '{folderName}' " +
                $"(hash: {payload.PayloadHash}) before UI builds.");

            ApplyPayload(payload);
        }
        catch (Exception ex)
        {
            SoulLogger.Warning(LOG_SOURCE,
                $"Failed to pre-apply cached sync for '{folderName}': {ex.Message}");
        }
    }

    static void ProcessAccumulatedChunks()
    {
        if (_chunks.Count == 0)
        {
            SoulLogger.Warning(LOG_SOURCE,
                "Received [[LG:end]] but chunk list is empty — ignoring.");
            return;
        }

        var json = string.Concat(_chunks);
        _chunks.Clear();

        try
        {
            var payload = JsonSerializer.Deserialize<ServerSyncPayload>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload == null)
            {
                SoulLogger.Warning(LOG_SOURCE,
                    "Deserialized payload is null — ignoring.");
                return;
            }

            SoulLogger.Info(LOG_SOURCE,
                $"Sync payload received from '{payload.ServerIdentity}' " +
                $"(hash: {payload.PayloadHash}, " +
                $"appearances: {payload.ItemAppearanceOverrides.Count}, " +
                $"recipes: {payload.RecipeOverrides.Count}).");

            if (!string.IsNullOrEmpty(_connectionString))
                ServerRegistry.Register(_connectionString, payload.ServerIdentity);

            WriteToDiskIfChanged(payload);

            if (_clientWorldReady)
                ApplyPayload(payload);
            else
                _pendingPayload = payload;
        }
        catch (Exception ex)
        {
            SoulLogger.Error(LOG_SOURCE,
                $"Failed to process sync payload: {ex.Message}");
            _chunks.Clear();
        }
    }

    static void ApplyPayload(ServerSyncPayload payload)
    {
        // 1–2. Display names — repoint via LocalizationPatcher.
        // [CHANGED] Replaces LocalizationInjector.Inject(payload). Clear
        //           restores prior repointed names; Apply mints fresh keys
        //           and repoints. No LoadDefaultLanguage — nothing wipes
        //           minted keys. Tooltips are NOT handled here (pending
        //           Harmony patch); Tooltip overrides are currently no-ops.
        LocalizationPatcher.ClearPrevious();
        LocalizationPatcher.Apply(payload);

        // 3–4. Icons — sprites into ManagedItemData.Icon.
        // ClearPrevious restores original icons before applying new ones.
        IconPatcher.ClearPrevious();
        IconPatcher.Apply(payload);

        // 5. Recipe patching — ingredients, outputs, craft duration.
        RecipePatcher.Apply(payload.RecipeOverrides);

        // 6. Station recipe patching — WorkstationRecipesBuffer on placed stations.
        RecipePatcher.ApplyStationRecipes(payload.StationRecipeOverrides);

        // 7. Player recipe patching — add/remove from client player entity buffer.
        RecipePatcher.ApplyPlayerRecipes(
            payload.PlayerRecipesToAdd,
            payload.PlayerRecipesToRemove);
    }

    static void WriteToDiskIfChanged(ServerSyncPayload payload)
    {
        var syncFile = SoulPathIndex.SyncFile(payload.ServerIdentity);

        if (File.Exists(syncFile))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<ServerSyncPayload>(
                    File.ReadAllText(syncFile),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (existing?.PayloadHash == payload.PayloadHash)
                {
                    SoulLogger.Debug(LOG_SOURCE,
                        $"Payload hash unchanged ({payload.PayloadHash}) " +
                        "— skipping disk write.");
                    return;
                }
            }
            catch
            {
                // Malformed cache — overwrite below.
            }
        }

        try
        {
            Directory.CreateDirectory(SoulPathIndex.ServerDir(payload.ServerIdentity));
            File.WriteAllText(syncFile,
                JsonSerializer.Serialize(payload,
                    new JsonSerializerOptions { WriteIndented = true }));

            SoulLogger.Info(LOG_SOURCE,
                $"Sync payload cached to '{syncFile}'.");
        }
        catch (Exception ex)
        {
            SoulLogger.Warning(LOG_SOURCE,
                $"Failed to write sync payload to disk: {ex.Message}");
        }
    }
}