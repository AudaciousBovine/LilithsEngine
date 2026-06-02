using HarmonyLib;
using ProjectM;
using LilithsSoul.Foundation;
using LilithsSoul.Network;
using LilithsSoul.Services;

// ============================================================
//  ClientInitPatch — LilithsSoul
//
//  Detects when the client ECS world and prefabs are fully loaded.
//
//  Hook target: GameDataManager.OnUpdate (postfix, single-fire)
//
//  [CHANGED] Reads ClientBootstrapSystem.ConnectionString and passes
//            it to SyncReceiver.NotifyWorldReady(). This lets
//            SyncReceiver look up the server in servers.json and
//            pre-apply the cached sync.json BEFORE CharacterHUD builds
//            — eliminating the UI timing race where the server payload
//            arrives after the UI is already built.
//
//  [CHANGED] Removed the temporary RepointDiagnostic probe (and its
//            LilithsSoul.Services using). The tooltip-build target it
//            was hunting for (SomeReusableSubMenuThings.RefreshGeneral-
//            ItemTooltip) has been identified and is now patched by
//            ItemDescriptionPatch. RepointDiagnostic.cs can be deleted.
//
//  [PERFORMANCE] Guard check is a single bool read per frame until
//                initialization — negligible. After first fire the
//                patch is effectively free.
// ============================================================

namespace LilithsSoul.Patches;

[HarmonyPatch(typeof(GameDataManager), nameof(GameDataManager.OnUpdate))]
internal static class ClientInitPatch
{
    private const string LOG_SOURCE = "LilithsSoul.ClientInitPatch";

    static bool _initialized;

    [HarmonyPostfix]
    static void Postfix(GameDataManager __instance)
    {
        if (_initialized) return;
        if (!__instance.GameDataInitialized) return;

        _initialized = true;

        try
        {
            SoulLogger.Info(LOG_SOURCE, "Client world ready — game data initialized.");

            // Read the current server connection string from ClientBootstrapSystem
            // so SyncReceiver can look up the cached sync.json before the server
            // payload arrives. ConnectionString is populated before OnUpdate fires
            // since the client must already be connecting.
            //
            // [PERFORMANCE] One system lookup — negligible.
            string connectionString = string.Empty;

            try
            {
                var world = Soul.ClientWorld;
                if (world != null)
                {
                    var bootstrap = world.GetExistingSystemManaged<ClientBootstrapSystem>();
                    if (bootstrap != null)
                    {
                        connectionString = bootstrap.ConnectionString ?? string.Empty;
                        SoulLogger.Info(LOG_SOURCE,
                            $"Connection string: '{connectionString}'");
                    }
                    else
                    {
                        SoulLogger.Warning(LOG_SOURCE,
                            "ClientBootstrapSystem not found — cannot read connection string.");
                    }
                }
            }
            catch (Exception ex)
            {
                SoulLogger.Warning(LOG_SOURCE,
                    $"Could not read ConnectionString: {ex.Message}");
            }

            SyncReceiver.NotifyWorldReady(connectionString);
        }
        catch (Exception ex)
        {
            SoulLogger.Error(LOG_SOURCE, $"ClientInitPatch failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets the initialization flag on client disconnect.
    /// </summary>
    internal static void Reset() => _initialized = false;
}