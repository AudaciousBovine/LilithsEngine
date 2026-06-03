// ============================================================
//  CookbookConfig — LilithsCookbook
//  LilithsCookbook/Config/CookbookConfig.cs
//
//  BepInEx config bindings for LilithsCookbook.
//
//  [CHANGED] GeneratePrisonerFeedExample flag added.
//            When enabled, writes prisoner-feed-example.json
//            with ChangesEnabled=true entries covering all three
//            FakeItem behaviour types (FeedPrisoner,
//            AffectWithToxic, DealDamageToPrisoner) plus their
//            corresponding feed recipes, so admins can verify
//            the prisoner feeding system is working end-to-end.
//            Auto-resets to false after generation, same pattern
//            as GenerateAllRecipes.
//
//  [PERFORMANCE] All values read directly from ConfigEntry.Value.
//                No Lazy<T> wrappers. BepInEx caches parsed values.
// ============================================================

using BepInEx.Configuration;
using LilithsHeart.Foundation;

namespace LilithsCookbook.Config;

public static class CookbookConfig
{
    private const string LOG_SOURCE = "LilithsCookbook.CookbookConfig";

    static ConfigEntry<bool> _generateAllRecipes          = null!;
    static ConfigEntry<bool> _generatePrisonerFeedExample = null!;

    public static bool GenerateAllRecipes          => _generateAllRecipes.Value;
    public static bool GeneratePrisonerFeedExample => _generatePrisonerFeedExample.Value;

    public static void Initialize(ConfigFile config)
    {
        _generateAllRecipes = config.Bind(
            section:      "Generation",
            key:          "GenerateAllRecipes",
            defaultValue: false,
            description:  "When set to true, generates a JSON file containing all existing vanilla recipes " +
                          "with ChangesEnabled set to false. The file will be written to " +
                          "BepInEx/config/LilithsHeart/Recipes/all-recipes.json on next boot. " +
                          "This setting will automatically reset to false after generation."
        );

        // [CHANGED] New flag — generates prisoner-feed-example.json with live
        //           test entries for all three FakeItem behaviour types.
        _generatePrisonerFeedExample = config.Bind(
            section:      "Generation",
            key:          "GeneratePrisonerFeedExample",
            defaultValue: false,
            description:  "When set to true, generates prisoner-feed-example.json in the Recipes directory. " +
                          "The file contains ChangesEnabled=true entries covering all three prisoner " +
                          "FakeItem behaviour types (FeedPrisoner, AffectWithToxic, DealDamageToPrisoner) " +
                          "plus their corresponding feed recipes, with values visibly different from vanilla " +
                          "so you can confirm the system is working in-game. " +
                          "This setting will automatically reset to false after generation."
        );

        HeartLogger.Info(LOG_SOURCE,
            $"CookbookConfig loaded. " +
            $"GenerateAllRecipes={GenerateAllRecipes}, " +
            $"GeneratePrisonerFeedExample={GeneratePrisonerFeedExample}");
    }

    /// <summary>
    /// Resets GenerateAllRecipes to false after generation completes.
    /// Called automatically by CookbookConfigBuilder.
    /// </summary>
    public static void DisableGenerateAllRecipes()
    {
        _generateAllRecipes.Value = false;
        HeartLogger.Info(LOG_SOURCE, "GenerateAllRecipes reset to false.");
    }

    /// <summary>
    /// [CHANGED] Resets GeneratePrisonerFeedExample to false after generation completes.
    /// Called automatically by CookbookConfigBuilder.
    /// </summary>
    public static void DisableGeneratePrisonerFeedExample()
    {
        _generatePrisonerFeedExample.Value = false;
        HeartLogger.Info(LOG_SOURCE, "GeneratePrisonerFeedExample reset to false.");
    }
}