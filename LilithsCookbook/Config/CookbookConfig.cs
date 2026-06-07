// ============================================================
//  CookbookConfig — LilithsCookbook
//  LilithsCookbook/Config/CookbookConfig.cs
//
//  [CHANGED] Added RunComponentAddTest and RunComponentRemoveTest
//            experiment flags in section "3) Debug".
//            Both default to false — opt-in, same as all debug flags.
//            Both have Disable*() methods matching the existing pattern.
//            These flags gate CookbookExperimentService.RunAddTest()
//            and RunRemoveTest() respectively.
// ============================================================

using BepInEx.Configuration;
using LilithsHeart.Foundation;

namespace LilithsCookbook.Config;

public static class CookbookConfig
{
    private const string LOG_SOURCE = "LilithsCookbook.CookbookConfig";

    static ConfigEntry<bool> _moduleEnabled                = null!;
    static ConfigEntry<bool> _generateAllRecipes           = null!;
    static ConfigEntry<bool> _generateCookbookExamples     = null!;
    static ConfigEntry<bool> _generateCookbookDebugConfigs = null!;

    // [ADDED] Experiment flags — gate CookbookExperimentService tests.
    static ConfigEntry<bool> _runComponentAddTest    = null!;
    static ConfigEntry<bool> _runComponentRemoveTest = null!;

    public static bool ModuleEnabled                => _moduleEnabled.Value;
    public static bool GenerateAllRecipes           => _generateAllRecipes.Value;
    public static bool GenerateCookbookExamples     => _generateCookbookExamples.Value;
    public static bool GenerateCookbookDebugConfigs => _generateCookbookDebugConfigs.Value;

    // [ADDED] Experiment flag accessors.
    public static bool RunComponentAddTest    => _runComponentAddTest.Value;
    public static bool RunComponentRemoveTest => _runComponentRemoveTest.Value;

    public static void Initialize(ConfigFile config)
    {
        _moduleEnabled = config.Bind(
            section:      "1) General",
            key:          "ModuleEnabled",
            defaultValue: true,
            description:  "When false, LilithsCookbook is completely disabled. " +
                          "Restart the server after changing this value for it to take effect."
        );

        _generateCookbookExamples = config.Bind(
            section:      "2) Config Generation",
            key:          "GenerateCookbookExamples",
            defaultValue: false,
            description:  "Generates Cookbook example config files: " +
                          "RecipeExamples, PrisonerFeedExamples, PrisonerFedExamples, " +
                          "and CookbookItemExamples to show formatting, use them as a base for your " +
                          "own changes. " +
                          "Always overwrites. Resets to false after generation."
        );

        _generateAllRecipes = config.Bind(
            section:      "3) Debug",
            key:          "GenerateAllRecipes",
            defaultValue: false,
            description:  "Generates a file with all vanilla recipes in Recipes/AllRecipes.json " +
                          "with ChangesEnabled=false. Use as a reference when making recipe changes " +
                          "but remove from Recipes when starting server or it may disable your changes. " +
                          "Always overwrites itself. Resets to false after generation."
        );

        _generateCookbookDebugConfigs = config.Bind(
            section:      "3) Debug",
            key:          "GenerateCookbookDebugConfigs",
            defaultValue: false,
            description:  "Generates Cookbook debug config files. " +
                          "Use to verify Cookbook features are working in-game. " +
                          "Always overwrites. Resets to false after generation."
        );

        // [ADDED] RunComponentAddTest — gates Test A in CookbookExperimentService.
        // Attempts to add ProjectM.AffectPrisonerWithToxic to FakeItem_FeedPrisoner_Rat
        // and remove ProjectM.ConsumableCondition from the same entity.
        // Results logged with [EXPERIMENT] prefix for easy grepping.
        // Resets to false after running.
        _runComponentAddTest = config.Bind(
            section:      "3) Debug",
            key:          "RunComponentAddTest",
            defaultValue: false,
            description:  "EXPERIMENT: Attempts to add a structural ECS component " +
                          "(ProjectM.AffectPrisonerWithToxic) to FakeItem_FeedPrisoner_Rat, " +
                          "and remove a zero-size tag (ProjectM.ConsumableCondition) from the same entity. " +
                          "Results logged with [EXPERIMENT] prefix. " +
                          "Resets to false after running. Dev use only."
        );

        // [ADDED] RunComponentRemoveTest — gates Test B in CookbookExperimentService.
        // Attempts to remove ProjectM.RecipeOutputUnitBuffer (empty) from
        // Recipe_Weapon_Sword_T01_Bone and verifies RecipeData and
        // RecipeHashLookupMap survive the archetype change.
        // Results logged with [EXPERIMENT] prefix for easy grepping.
        // Resets to false after running.
        _runComponentRemoveTest = config.Bind(
            section:      "3) Debug",
            key:          "RunComponentRemoveTest",
            defaultValue: false,
            description:  "EXPERIMENT: Attempts to remove ProjectM.RecipeOutputUnitBuffer " +
                          "(present but empty) from Recipe_Weapon_Sword_T01_Bone, then verifies " +
                          "RecipeData and RecipeHashLookupMap are intact after the archetype change. " +
                          "Results logged with [EXPERIMENT] prefix. " +
                          "Resets to false after running. Dev use only."
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

    // [ADDED] Disable methods for experiment flags — auto-reset after each run.

    public static void DisableRunComponentAddTest()
    {
        _runComponentAddTest.Value = false;
        HeartLogger.Info(LOG_SOURCE, "RunComponentAddTest reset to false.");
    }

    public static void DisableRunComponentRemoveTest()
    {
        _runComponentRemoveTest.Value = false;
        HeartLogger.Info(LOG_SOURCE, "RunComponentRemoveTest reset to false.");
    }
}