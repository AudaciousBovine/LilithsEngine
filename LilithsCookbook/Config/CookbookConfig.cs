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
        _moduleEnabled = config.Bind(
            section:      "1) General",
            key:          "ModuleEnabled",
            defaultValue: true,
            description:  "When false, LilithsCookbook is completely disabled. " +
                          "Restart the server after changing this value for it to take effect."
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

        _generateCookbookDebugConfigs = config.Bind(
            section:      "3) Debug",
            key:          "GenerateCookbookDebugConfigs",
            defaultValue: false,
            description:  "Generates Cookbook debug config files. " +
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