using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using LilithsSoul.Config;
using LilithsSoul.Foundation;
using LilithsSoul.Services;

// ============================================================
//  SoulPlugin — LilithsSoul
//
//  BepInEx entry point for the LilithsSoul client mod.
//
//  Load order:
//  ───────────
//  1. SoulLogger    — logging available immediately
//  2. SoulConfig    — reads LilithsSoul.cfg
//  3. Harmony       — patches:
//                       • ClientInitPatch     (world ready detection)
//                       • ClientChatSystemPatch (chunk interception)
//
//  [CHANGED] SyncReceiver.RegisterHandlers() removed.
//  Networking now uses ChatMessageServerEvent (system chat messages)
//  instead of Unity Netcode CustomMessagingManager. No handler
//  registration is needed — ClientChatSystemPatch intercepts
//  messages automatically via Harmony on ClientChatSystem.OnUpdate.
// ============================================================

namespace LilithsSoul;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class SoulPlugin : BasePlugin
{
    static Harmony? _harmony;

    public override void Load()
    {
        // [ADDED] Must be first — registers SoulCoroutineHost with IL2CPP
        // before any coroutine is started. Required by IconDownloader.
        SoulCoroutineHost.Register();

        SoulLogger.Initialize(base.Log);

        try
        {
            SoulLogger.Info("LilithsSoul",
                $"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} loading.");

            var configFile = new ConfigFile(SoulPathIndex.CoreConfig, saveOnInit: true);
            SoulConfig.Initialize(configFile);

            _harmony?.UnpatchSelf();
            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            _harmony.PatchAll();

            SoulLogger.Info("LilithsSoul", "LilithsSoul loaded.");
        }
        catch (Exception ex)
        {
            SoulLogger.Error("LilithsSoul", $"Failed to load: {ex.Message}");
        }
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        SystemLanguageResolver.Reset();
        SoulLogger.Info("LilithsSoul", "LilithsSoul unloaded.");
        return true;
    }
}