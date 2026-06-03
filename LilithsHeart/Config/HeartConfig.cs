// ============================================================
//  HeartConfig — LilithsHeart
//  LilithsHeart/Config/HeartConfig.cs
//
//  Master config file for LilithsHeart and the suite.
//
//  [CHANGED] Generation flags overhauled:
//    GenerateExampleConfigs  → split into GenerateHeartExamples
//                              and GenerateAllModuleExamples
//    GenerateDebugConfigs    → new, triggers all module debug generators
//    GenerateNameAliasConfigs → new, dumps compiled prefab aliases
//                               to Aliases/*.json for admin editing
//
//  [PERFORMANCE] All values read directly from ConfigEntry.Value.
//                No Lazy<T> wrappers.
// ============================================================

using BepInEx.Configuration;
using LilithsHeart.Foundation;

namespace LilithsHeart.Config;

public static class HeartConfig
{
    private const string LOG_SOURCE = "LilithsHeart.HeartConfig";

    static ConfigEntry<bool>   _debugLogging             = null!;
    static ConfigEntry<int>    _chunksPerFrame            = null!;
    static ConfigEntry<bool>   _generateHeartExamples     = null!;
    static ConfigEntry<bool>   _generateAllModuleExamples = null!;
    static ConfigEntry<bool>   _generateDebugConfigs      = null!;
    static ConfigEntry<bool>   _generateNameAliasConfigs  = null!;

    public static ConfigEntry<string> ServerName { get; private set; } = null!;

    public static bool IsDebug                  => _debugLogging.Value;
    public static int  ChunksPerFrame           => _chunksPerFrame.Value;
    public static bool GenerateHeartExamples    => _generateHeartExamples.Value;
    public static bool GenerateAllModuleExamples => _generateAllModuleExamples.Value;
    public static bool GenerateDebugConfigs     => _generateDebugConfigs.Value;
    public static bool GenerateNameAliasConfigs => _generateNameAliasConfigs.Value;

    public static void Initialize(ConfigFile config)
    {
        ServerName = config.Bind(
            section:      "1) General",
            key:          "ServerName",
            defaultValue: "LilithsGarden",
            description:  "Unique name for this server. Used by Soul clients to cache " +
                          "server-specific configs. Change this if you run multiple " +
                          "LilithsGarden servers."
        );

        _chunksPerFrame = config.Bind(
            section:      "2) Sync",
            key:          "ChunksPerFrame",
            defaultValue: 10,
            description:  "Maximum number of sync payload chunks sent per server frame. " +
                          "Higher values sync clients faster but increase CPU load on connect. " +
                          "Reduce if you see frame drops when many players connect simultaneously. " +
                          "Default: 10. Range: 1-50."
        );

        _debugLogging = config.Bind(
            section:      "3) Debug",
            key:          "DebugLogging",
            defaultValue: false,
            description:  "Enable verbose debug logging for LilithsHeart. " +
                          "Useful during development — disable on live servers."
        );

        // [CHANGED] GenerateHeartExamples — generates only Heart's own
        //           Items/ItemExamples.json (DisplayName, DescriptionText, Icon).
        //           Does not trigger module generators. Always overwrites.
        _generateHeartExamples = config.Bind(
            section:      "4) Config Generation",
            key:          "GenerateHeartExamples",
            defaultValue: false,
            description:  "Generates Items/ItemExamples.json showing Heart's appearance " +
                          "fields (DisplayName, DescriptionText, Icon). " +
                          "Always overwrites the existing file. Resets to false after generation."
        );

        // [CHANGED] GenerateAllModuleExamples — merges Heart's item examples with
        //           all registered module item contributions into Items/ItemExamples.json,
        //           then triggers each module's own example generator.
        //           Always overwrites. Takes priority over GenerateHeartExamples
        //           if both are set.
        _generateAllModuleExamples = config.Bind(
            section:      "4) Config Generation",
            key:          "GenerateAllModuleExamples",
            defaultValue: false,
            description:  "Merges Heart's item appearance examples with all installed " +
                          "module item contributions into one Items/ItemExamples.json, " +
                          "then triggers each module's own example file generation. " +
                          "Always overwrites. Takes priority over GenerateHeartExamples. " +
                          "Resets to false after generation."
        );

        // [CHANGED] GenerateDebugConfigs — triggers all registered module debug generators.
        _generateDebugConfigs = config.Bind(
            section:      "4) Config Generation",
            key:          "GenerateDebugConfigs",
            defaultValue: false,
            description:  "Triggers debug config generation for all installed modules. " +
                          "Debug configs have ChangesEnabled=true with values obviously " +
                          "different from vanilla — use to verify features are working. " +
                          "Always overwrites. Resets to false after generation."
        );

        // [CHANGED] GenerateNameAliasConfigs — dumps compiled PrefabDef aliases
        //           to Aliases/*.json so admins can override them per server.
        _generateNameAliasConfigs = config.Bind(
            section:      "4) Config Generation",
            key:          "GenerateNameAliasConfigs",
            defaultValue: false,
            description:  "Dumps all compiled prefab Name aliases from LilithsMind to " +
                          "Aliases/*.json (one file per index class). Admins can edit " +
                          "these files to use custom aliases in all module configs. " +
                          "Always overwrites. Resets to false after generation."
        );

        HeartLogger.Info(LOG_SOURCE,
            $"HeartConfig loaded. Debug={IsDebug}, ChunksPerFrame={ChunksPerFrame}");
    }

    public static void DisableGenerateHeartExamples()
    {
        _generateHeartExamples.Value = false;
        HeartLogger.Info(LOG_SOURCE, "GenerateHeartExamples reset to false.");
    }

    public static void DisableGenerateAllModuleExamples()
    {
        _generateAllModuleExamples.Value = false;
        HeartLogger.Info(LOG_SOURCE, "GenerateAllModuleExamples reset to false.");
    }

    public static void DisableGenerateDebugConfigs()
    {
        _generateDebugConfigs.Value = false;
        HeartLogger.Info(LOG_SOURCE, "GenerateDebugConfigs reset to false.");
    }

    public static void DisableGenerateNameAliasConfigs()
    {
        _generateNameAliasConfigs.Value = false;
        HeartLogger.Info(LOG_SOURCE, "GenerateNameAliasConfigs reset to false.");
    }
}