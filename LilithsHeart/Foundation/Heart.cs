using ProjectM;
using Unity.Entities;
using LilithsHeart.Config;
using LilithsHeart.Events;
using LilithsHeart.Network;
using LilithsHeart.Modules;
using LilithsHeart.Services;
using LilithsMind.Data;    // SyncModeEnum
using LilithsMind.Network; // LilithRecipeData, LilithStationData, ServerSyncPayload

namespace LilithsHeart.Foundation;

// ============================================================
//  Heart — LilithsHeart
//
//  Central access point for server ECS systems and the module
//  registration/sync pipeline.
//
//  Lifecycle:
//  ──────────
//  1. OnInitialize() fires via WarEventRegistrySystem patch.
//  2. PrefabNameResolver and LilithItemConfig are initialized.
//  3. A baseline SyncPayloadCache is built (empty overrides).
//  4. OnInitialized event fires — registered modules apply their
//     changes and call Register*() to queue overrides.
//  5. Payload is rebuilt with all accumulated overrides.
//  6. OnWorldReady event fires for late subscribers.
//
//  [CHANGED] LocalizationService.RegisterDirectory/Initialize replaced
//            by ItemService (file I/O owner) + LocalizationService,
//            InterfaceService, and ItemFunctionalService (apply layers).
//
//  [PERFORMANCE] System accessors (PrefabCollectionSystem,
//                GameDataSystem) are fetched per-call via Unity's
//                GetExistingSystemManaged — cached internally by Unity.
//                No static system references that could go stale.
// ============================================================

public static class Heart
{
    private const string LOG_SOURCE = "LilithsHeart";

    static World? _server;
    static World Server
    {
        get
        {
            if (_server?.IsCreated == true)
                return _server;
            _server = WorldUtility.FindServerWorld();
            if (_server?.IsCreated != true)
                throw new InvalidOperationException("Server world is not ready yet.");
            return _server;
        }
    }

    public static EntityManager EntityManager
        => Server.EntityManager;

    public static PrefabCollectionSystem PrefabCollectionSystem
        => Server.GetExistingSystemManaged<PrefabCollectionSystem>();

    public static GameDataSystem GameDataSystem
        => Server.GetExistingSystemManaged<GameDataSystem>();

    public static event Action? OnInitialized;
    static bool _initialized;
    public static bool IsReady => _initialized;

    static string _serverIdentity = string.Empty;

    static readonly Dictionary<string, LilithRecipeData>  _pendingRecipeOverrides        = new();
    static readonly Dictionary<string, LilithStationData> _pendingStationRecipeOverrides  = new();
    static readonly List<string>                           _pendingPlayerRecipesToAdd      = new();
    static readonly List<string>                           _pendingPlayerRecipesToRemove   = new();

    // ── Lifecycle ─────────────────────────────────────────────

    internal static void OnInitialize()
    {
        if (_initialized) return;

        HeartLogger.Info(LOG_SOURCE, "Heart initializing...");

        PrefabNameResolver.Initialize();

        // [CHANGED] HeartConfigBuilder.RunIfRequested() replaces GenerateIfRequested().
        //           Handles GenerateHeartExamples, GenerateAllModuleExamples,
        //           and GenerateDebugConfigs based on which flags are set.
        //           GenerateAllModuleExamples takes priority over GenerateHeartExamples.
        HeartConfigBuilder.RunIfRequested();

        // [CHANGED] ItemService is the single owner of Items/*.json file I/O.
        //           It populates LilithItemConfig's two dictionaries in one pass.
        //           LocalizationService, InterfaceService, and ItemFunctionalService
        //           are pure apply-layers that read from LilithItemConfig.
        ItemService.RegisterDirectory(HeartPathIndex.ItemsDir);
        ItemService.Initialize();

        // Apply-layer services — read from LilithItemConfig, no file I/O.
        // Note: ItemFunctionService (StackSize) lives in LilithsCookbook and is
        //       called from CookbookPlugin.OnHeartInitialized(), not here.
        LocalizationService.Initialize();
        InterfaceService.Initialize();

        // [CHANGED] Load per-language overrides from Localization/ subfolders.
        //           Must run after ItemService so the default language overrides
        //           are already loaded (for reference/comparison). Results are
        //           cached in LocalizationFileService and served on demand when
        //           Soul clients request a specific language.
        LocalizationFileService.Initialize();

        _serverIdentity = ResolveServerIdentity();

        // [CHANGED] Start SyncHttpServer if mode requires it.
        // Must start before the payload is built so the endpoint
        // is ready by the time clients can connect.
        if (HeartConfig.SyncMode == LilithsMind.Data.SyncModeEnum.HttpServer)
            LilithsHeart.Network.SyncHttpServer.Start();

        // Build a baseline payload before modules register overrides.
        SyncPayloadCache.Rebuild(_serverIdentity,
            _pendingRecipeOverrides,
            _pendingStationRecipeOverrides,
            _pendingPlayerRecipesToAdd,
            _pendingPlayerRecipesToRemove);

        _initialized = true;

        HeartLogger.Info(LOG_SOURCE, "Heart initialized.");

        // Fire OnInitialized — modules apply their changes and call Register*().
        OnInitialized?.Invoke();

        // Rebuild payload after all modules have registered their overrides.
        bool needsRebuild = _pendingRecipeOverrides.Count       > 0 ||
                            _pendingStationRecipeOverrides.Count > 0 ||
                            _pendingPlayerRecipesToAdd.Count     > 0 ||
                            _pendingPlayerRecipesToRemove.Count  > 0;

        if (needsRebuild)
        {
            HeartLogger.Info(LOG_SOURCE,
                $"Rebuilding sync payload with " +
                $"{_pendingRecipeOverrides.Count} recipe override(s), " +
                $"{_pendingStationRecipeOverrides.Count} station override(s), " +
                $"{_pendingPlayerRecipesToAdd.Count} player add(s), " +
                $"{_pendingPlayerRecipesToRemove.Count} player remove(s).");

            SyncPayloadCache.Rebuild(_serverIdentity,
                _pendingRecipeOverrides,
                _pendingStationRecipeOverrides,
                _pendingPlayerRecipesToAdd,
                _pendingPlayerRecipesToRemove);
        }

        HeartModuleRegistry.LogSummary();
        HeartEventBus.Publish(new OnWorldReady());
    }

    // [CHANGED] OnLocalizationReloaded is triggered by ItemService.Reload()
    //           rather than LocalizationService.Reload() (which no longer exists).
    internal static void OnLocalizationReloaded()
        => SyncPayloadCache.Rebuild(_serverIdentity,
            _pendingRecipeOverrides,
            _pendingStationRecipeOverrides,
            _pendingPlayerRecipesToAdd,
            _pendingPlayerRecipesToRemove);

    internal static void OnDestroy()
    {
        _initialized    = false;
        _server         = null;
        _serverIdentity = string.Empty;
        _pendingRecipeOverrides.Clear();
        _pendingStationRecipeOverrides.Clear();
        _pendingPlayerRecipesToAdd.Clear();
        _pendingPlayerRecipesToRemove.Clear();
        OnInitialized = null;

        // [ADDED] Clear any pending sync chunks for all clients.
        //         Prevents stale chunks from a previous world session
        //         being sent on the next world load.
        SyncQueue.Clear();

        // [CHANGED] Stop SyncHttpServer if it was running.
        LilithsHeart.Network.SyncHttpServer.Stop();

        HeartLogger.Info(LOG_SOURCE, "Heart destroyed.");
    }

    // ── Module registration API ───────────────────────────────

    /// <summary>
    /// Called by RecipeSystem after applying recipe ECS changes.
    /// Queues overrides for inclusion in the next ServerSyncPayload build.
    /// [PERFORMANCE] Called once at startup per module — O(n) over overrides.
    /// </summary>
    public static void RegisterRecipeOverrides(Dictionary<string, LilithRecipeData> overrides)
    {
        foreach (var (key, value) in overrides)
            _pendingRecipeOverrides[key] = value;

        HeartLogger.Debug(LOG_SOURCE,
            $"RegisterRecipeOverrides: +{overrides.Count} entries, total={_pendingRecipeOverrides.Count}");
    }

    /// <summary>
    /// Called by StationSystem after patching WorkstationRecipesBuffer station entities.
    /// [PERFORMANCE] Called once at startup per WorkstationRecipesBuffer station.
    /// </summary>
    public static void RegisterStationRecipeChanges(string stationName, List<string> toAdd, List<string> toRemove)
    {
        if (!_pendingStationRecipeOverrides.TryGetValue(stationName, out var existing))
        {
            existing = new LilithStationData();
            _pendingStationRecipeOverrides[stationName] = existing;
        }

        foreach (var r in toAdd)
            if (!existing.RecipesToAdd.Contains(r))
                existing.RecipesToAdd.Add(r);

        foreach (var r in toRemove)
            if (!existing.RecipesToRemove.Contains(r))
                existing.RecipesToRemove.Add(r);

        HeartLogger.Debug(LOG_SOURCE,
            $"RegisterStationRecipeChanges: '{stationName}' +{toAdd.Count} add, +{toRemove.Count} remove.");
    }

    /// <summary>
    /// Called by StationSystem after patching live User entities.
    /// [PERFORMANCE] Called once at startup per player crafting entry.
    /// </summary>
    public static void RegisterPlayerRecipeChanges(List<string> toAdd, List<string> toRemove)
    {
        foreach (var r in toAdd)
            if (!_pendingPlayerRecipesToAdd.Contains(r))
                _pendingPlayerRecipesToAdd.Add(r);

        foreach (var r in toRemove)
            if (!_pendingPlayerRecipesToRemove.Contains(r))
                _pendingPlayerRecipesToRemove.Add(r);

        HeartLogger.Debug(LOG_SOURCE,
            $"RegisterPlayerRecipeChanges: +{toAdd.Count} add, +{toRemove.Count} remove.");
    }

    // ── Internal helpers ──────────────────────────────────────

    static string ResolveServerIdentity()
    {
        var name = HeartConfig.ServerName.Value;
        return string.IsNullOrWhiteSpace(name) ? "LilithsGarden" : name;
    }
}