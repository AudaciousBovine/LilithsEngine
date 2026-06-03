using LilithsHeart.Config;
using LilithsHeart.Foundation;

// ============================================================
//  HeartConfigBuilder — LilithsHeart
//  LilithsHeart/Services/HeartConfigBuilder.cs
//
//  Generates example config files for all installed modules.
//  Called from Heart.OnInitialize() before LocalizationService
//  loads, so fresh examples are immediately picked up.
//
//  Registration pattern:
//  ──────────────────────
//  Heart registers its own generator at startup.
//  Child modules register theirs in Load() before OnInitialized
//  fires so they're included when GenerateIfRequested() runs:
//
//      HeartConfigBuilder.RegisterGenerator(GenerateCookbookExamples);
//
//  GenerateIfRequested() checks HeartConfig.GenerateExampleConfigs
//  and calls all registered generators if true. The flag resets
//  to false automatically after all generators run.
//
//  Each generator is responsible for:
//    - Checking if its example file already exists (skip if so)
//    - Creating its target directory
//    - Writing the example JSON
//
//  [CHANGED] GenerateLocalizationExample → GenerateExampleConfigs.
//            Single flag now triggers all registered generators,
//            not just the localization example.
//
//  [CHANGED] Example JSON "Tooltip" key renamed to "DescriptionText"
//            to match the LilithItemData field rename. The client
//            applies this as the item's tooltip-body (description) text
//            via the ItemDescriptionPatch Harmony postfix — it is NOT
//            written through any localization key. (No back-compat shim
//            needed: no live servers have old "Tooltip" config files.)
//
//  [PERFORMANCE] Zero cost on normal boots — all work gated behind
//                the GenerateExampleConfigs flag check.
// ============================================================

namespace LilithsHeart.Services;

public static class HeartConfigBuilder
{
    private const string LOG_SOURCE = "LilithsHeart.HeartConfigBuilder";

    // Registered example generators from Heart and child modules.
    // [PERFORMANCE] Small list — populated once at startup.
    static readonly List<Action> _generators = [];

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Registers an example file generator.
    /// Called by Heart core and child modules during Load().
    /// Each generator should check if its file already exists
    /// and skip gracefully if so.
    /// </summary>
    public static void RegisterGenerator(Action generator)
    {
        if (generator != null)
            _generators.Add(generator);
    }

    /// <summary>
    /// Checks HeartConfig.GenerateExampleConfigs and runs all
    /// registered generators if true. Resets the flag to false
    /// after all generators complete.
    /// Called by Heart.OnInitialize() before LocalizationService.
    /// </summary>
    public static void GenerateIfRequested()
    {
        if (!HeartConfig.GenerateExampleConfigs) return;

        HeartLogger.Info(LOG_SOURCE,
            $"GenerateExampleConfigs is true — running {_generators.Count} generator(s).");

        foreach (var generator in _generators)
        {
            try
            {
                generator();
            }
            catch (Exception ex)
            {
                HeartLogger.Error(LOG_SOURCE,
                    $"Generator failed: {ex.Message}");
            }
        }

        HeartConfig.DisableGenerateExampleConfigs();
    }

    // ── Built-in generators ───────────────────────────────────

    /// <summary>
    /// Generates Items/example.json demonstrating all three icon
    /// methods alongside display name and description overrides.
    /// Registered by Heart.OnInitialize() as the core generator.
    /// Skipped if the file already exists.
    /// </summary>
    public static void GenerateItemsExample()
    {
        var itemsDir    = HeartPathIndex.ItemsDir;
        var examplePath = Path.Combine(itemsDir, "example.json");

        if (File.Exists(examplePath))
        {
            HeartLogger.Info(LOG_SOURCE,
                $"Items example already exists at '{examplePath}' — skipping.");
            return;
        }

        const string example = """
{
  "_readme": "Keys are the prefab Name or Prefab string from LilithsMind PrefabDef entries (e.g. 'BloodEssence' or 'Item_BloodEssence_T01'). All fields are optional — omit any you do not want to change. Files in subdirectories are included automatically (e.g. Items/Currencies/). Files load in full-path alphabetical order — later files win per-field on key conflicts.",

  "_icon_readme": "Icon can be set three ways: (1) a PNG filename resolved from the client's Icons/ folder — e.g. 'vitae.png'; (2) an in-game sprite name from Resources — e.g. 'Icon_BloodOrb'; (3) an https:// URL the client will download and cache to their Icons/ folder.",

  "_description_readme": "DescriptionText sets the item's tooltip body text. The client overrides it per-item at hover time (no localization key is touched), so two items that share a vanilla description stay independent.",

  "Item_BloodEssence_T01": {
    "_comment": "Example 1: rename + description + local PNG icon (client must have vitae.png in their Icons/ folder)",
    "DisplayName": "Vitae",
    "DescriptionText": "Concentrated life force, harvested from the living.",
    "Icon": "vitae.png"
  },

  "Item_Ingredient_Gem_Ruby_T01": {
    "_comment": "Example 2: swap with an existing in-game sprite by its sprite name",
    "DisplayName": "Bloodstone",
    "Icon": "Icon_BloodOrb"
  },

  "Item_MagicSource_BloodKey_T01": {
    "_comment": "Example 3: use a hosted image — client downloads once and caches to Icons/",
    "DisplayName": "Crimson Key",
    "Icon": "https://example.com/icons/crimson-key.png"
  }
}
""";

        try
        {
            Directory.CreateDirectory(itemsDir);
            File.WriteAllText(examplePath, example);
            HeartLogger.Info(LOG_SOURCE,
                $"Generated Items example at '{examplePath}'.");
        }
        catch (Exception ex)
        {
            HeartLogger.Warning(LOG_SOURCE,
                $"Could not write Items example: {ex.Message}");
        }
    }
}