using BepInEx.Configuration;
using LilithsHeart.Foundation;

// ============================================================
//  HeartConfig — LilithsHeart
//  LilithsHeart/Config/HeartConfig.cs
//
//  BepInEx config bindings for LilithsHeart core settings.
//
//  [CHANGED] Removed Lazy<T> wrappers — ConfigEntry<T>.Value
//            already caches after first read. Lazy<T> introduced
//            a hot-reload bug where re-reads never fired.
//
//  [CHANGED] Removed StartingInventorySize and
//            GlobalPlayerMovementSpeedMultiplier — gameplay
//            settings belong in the module that owns the feature.
//
//  [CHANGED] GenerateLocalizationExample renamed to
//            GenerateExampleConfigs. Now triggers example
//            generation for all installed features via
//            HeartConfigBuilder, not just localization.
//            DisableGenerateLocalizationExample() renamed to
//            DisableGenerateExampleConfigs() to match.
//
//  [ADDED] ChunksPerFrame — controls the tiered sync send rate.
// ============================================================

namespace LilithsHeart.Config;

public static class HeartConfig
{
    private const string LOG_SOURCE = "LilithsHeart.HeartConfig";

    static ConfigEntry<bool> _debugLogging          = null!;
    static ConfigEntry<bool> _generateExampleConfigs = null!;
    static ConfigEntry<int>  _chunksPerFrame         = null!;

    public static ConfigEntry<string> ServerName { get; private set; } = null!;

    public static bool IsDebug        => _debugLogging.Value;
    public static int  ChunksPerFrame => _chunksPerFrame.Value;

    /// <summary>
    /// When true, HeartConfigBuilder generates example config files
    /// for all installed features on next boot. Resets to false
    /// automatically after generation completes.
    /// </summary>
    public static bool GenerateExampleConfigs => _generateExampleConfigs.Value;

    public static void Initialize(ConfigFile config)
    {
        ServerName = config.Bind(
            section:      "General",
            key:          "ServerName",
            defaultValue: "LilithsGarden",
            description:  "Unique name for this server. Used by Soul clients to cache " +
                          "server-specific configs. Change this if you run multiple " +
                          "LilithsGarden servers."
        );

        _generateExampleConfigs = config.Bind(
            section:      "Config Generation",
            key:          "GenerateExampleConfigs",
            defaultValue: false,
            description:  "When set to true, generates example config files for all " +
                          "installed LilithsGarden modules on next boot. Includes Items/, " +
                          "Recipes/, Stations/, and any other registered module directories. " +
                          "This setting resets to false automatically after generation."
        );

        _chunksPerFrame = config.Bind(
            section:      "Client Sync",
            key:          "ChunksPerFrame",
            defaultValue: 10,
            description:  "Maximum number of sync payload chunks sent per server frame. " +
                          "Higher values sync clients faster but increase CPU load on connect. " +
                          "Reduce if you see frame drops when many players connect simultaneously. " +
                          "Default: 10. Range: 1-50."
        );

            _debugLogging = config.Bind(
            section:      "Debug",
            key:          "DebugLogging",
            defaultValue: false,
            description:  "Enable verbose debug logging for LilithsHeart. " +
                          "Useful during development, disable on live servers."
        );

        HeartLogger.Info(LOG_SOURCE,
            $"HeartConfig loaded. Debug={IsDebug}, ChunksPerFrame={ChunksPerFrame}");
    }

    /// <summary>
    /// Resets GenerateExampleConfigs to false after generation completes.
    /// Called automatically by HeartConfigBuilder after all files are written.
    /// </summary>
    public static void DisableGenerateExampleConfigs()
    {
        _generateExampleConfigs.Value = false;
        HeartLogger.Info(LOG_SOURCE, "GenerateExampleConfigs reset to false.");
    }
}