// ============================================================
//  CookbookConfig — LilithsCookbook
//  LilithsCookbook/Config/CookbookConfig.cs
//
//  BepInEx config bindings for LilithsCookbook.
//
//  [CHANGED] Full overhaul to match the new suite-wide config
//            generation system:
//
//    ModuleEnabled               — new, skips all initialization if false
//    GenerateAllRecipes          — unchanged, dumps vanilla recipe ECS state
//    GenerateCookbookExamples    — replaces GeneratePrisonerFeedExample +
//                                  any other one-off example flags;
//                                  generates all Cookbook example files
//    GenerateCookbookDebugConfigs — new, generates all Cookbook debug files
//
//  [PERFORMANCE] All values read directly from ConfigEntry.Value.
//                No Lazy<T> wrappers.
// ============================================================

using BepInEx.Configuration;
using LilithsHeart.Foundation;

namespace LilithsCookbook.Config;

public static class CookbookConfig
{
    private const string LOG_SOURCE = "LilithsCookbook.CookbookConfig";

    static ConfigEntry<bool> _moduleEnabled              = null!;
    static ConfigEntry<bool> _generateAllRecipes         = null!;
    static ConfigEntry<bool> _generateCookbookExamples   = null!;
    static ConfigEntry<bool> _generateCookbookDebugConfigs = null!;

    public static bool ModuleEnabled               => _moduleEnabled.Value;
    public static bool GenerateAllRecipes          => _generateAllRecipes.Value;
    public static bool GenerateCookbookExamples    => _generateCookbookExamples.Value;
    public static bool GenerateCookbookDebugConfigs => _generateCookbookDebugConfigs.Value;

    public static void Initialize(ConfigFile config)
    {
        // [CHANGED] ModuleEnabled — when false, CookbookPlugin.Load() returns
        //           immediately after reading this value. No ECS patching,
        //           no registration, no Heart subscription.
        _moduleEnabled = config.Bind(
            section:      "1) General",
            key:          "ModuleEnabled",
            defaultValue: true,
            description:  "When false, LilithsCookbook is completely disabled. " +
                          "No recipe, station, prisoner feed, or item function changes " +
                          "will be applied. Restart the server after changing this value."
        );

        _generateAllRecipes = config.Bind(
            section:      "2) Config Generation",
            key:          "GenerateAllRecipes",
            defaultValue: false,
            description:  "Dumps all vanilla recipes from ECS to Recipes/AllRecipes.json " +
                          "with ChangesEnabled=false. Use as a reference when authoring " +
                          "recipe overrides. Always overwrites. Resets to false after generation."
        );

        // [CHANGED] GenerateCookbookExamples — replaces all previous one-off
        //           example flags. Generates all four Cookbook example files:
        //             Recipes/RecipeExamples.json
        //             Recipes/PrisonerFeedExamples.json
        //             Recipes/PrisonerFedExamples.json
        //             Items/CookbookItemExamples.json
        //           Always overwrites. Can also be triggered by Heart's
        //           GenerateAllModuleExamples.
        _generateCookbookExamples = config.Bind(
            section:      "2) Config Generation",
            key:          "GenerateCookbookExamples",
            defaultValue: false,
            description:  "Generates all Cookbook example config files: " +
                          "RecipeExamples, PrisonerFeedExamples, PrisonerFedExamples, " +
                          "and CookbookItemExamples. " +
                          "Always overwrites. Resets to false after generation."
        );

        // [CHANGED] GenerateCookbookDebugConfigs — generates debug variants of all
        //           Cookbook config files with ChangesEnabled=true and values
        //           obviously different from vanilla for feature verification.
        _generateCookbookDebugConfigs = config.Bind(
            section:      "2) Config Generation",
            key:          "GenerateCookbookDebugConfigs",
            defaultValue: false,
            description:  "Generates Cookbook debug config files with ChangesEnabled=true " +
                          "and values visibly different from vanilla. " +
                          "Use to verify Cookbook features are working in-game. " +
                          "Always overwrites. Resets to false after generation."
        );

        HeartLogger.Info(LOG_SOURCE,
            $"CookbookConfig loaded. ModuleEnabled={ModuleEnabled}");
    }

    public static void DisableGenerateAllRecipes()
    {
        _generateAllRecipes.Value = false;
        HeartLogger.Info(LOG_SOURCE, "GenerateAllRecipes reset to false.");
    }

    public static void DisableGenerateCookbookExamples()
    {
        _generateCookbookExamples.Value = false;
        HeartLogger.Info(LOG_SOURCE, "GenerateCookbookExamples reset to false.");
    }

    public static void DisableGenerateCookbookDebugConfigs()
    {
        _generateCookbookDebugConfigs.Value = false;
        HeartLogger.Info(LOG_SOURCE, "GenerateCookbookDebugConfigs reset to false.");
    }
}