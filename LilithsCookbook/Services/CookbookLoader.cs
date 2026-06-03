// ============================================================
//  CookbookLoader — LilithsCookbook
//  LilithsCookbook/Systems/CookbookLoader.cs
//
//  Reads and merges all *.json files from the Recipes directory
//  into runtime data containers.
//
//  [CHANGED] LoadStations() removed entirely. Station membership
//            is now declared inline on each RecipeEntryData via
//            its Stations list. CookbookStationData is retired.
//
//  [CHANGED] LoadPrisonerFeeding() added. Prisoner feed config
//            lives in the same *.json files as recipe config —
//            both CookbookRecipeData.Recipes and
//            CookbookPrisonerFeedData.PrisonerFeeding are
//            deserialized from the same file in one pass via a
//            combined wrapper type (CookbookRecipeFile), then
//            split into their respective containers.
//
//            This keeps one item = one file ergonomics for admins
//            while giving us two typed containers internally.
//
//  [PERFORMANCE] All files read once at startup. O(files) I/O,
//                O(entries) merge. No per-frame cost.
// ============================================================

using System.Text.Json;
using LilithsHeart.Foundation;
using LilithsCookbook.Data;

namespace LilithsCookbook.Services;

public static class CookbookLoader
{
    private const string LOG_SOURCE = "LilithsCookbook.CookbookLoader";

    static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads and merges all *.json files from the Recipes directory.
    /// Returns both the recipe overrides and prisoner feeding config
    /// parsed from the same files in one pass.
    ///
    /// Later files win on key collision within each container —
    /// admins can split config across as many files as they like.
    ///
    /// [CHANGED] No longer a separate LoadStations() call.
    ///           Station membership is read from RecipeEntryData.Stations.
    /// </summary>
    public static (CookbookRecipeData recipes, CookbookPrisonerFeedData feeding)
        LoadRecipes(string recipesDir)
    {
        var mergedRecipes  = new CookbookRecipeData();
        var mergedFeeding  = new CookbookPrisonerFeedData();

        foreach (var file in GetJsonFiles(recipesDir))
        {
            // Deserialize the combined file wrapper — both blocks are optional.
            // [PERFORMANCE] Single JsonSerializer.Deserialize per file — no
            //               double-parse. PropertyNameCaseInsensitive so admins
            //               can use any casing in their JSON keys.
            var incoming = Deserialize<CookbookRecipeFile>(file);
            if (incoming == null) continue;

            // Merge recipe entries.
            if (incoming.Recipes != null)
            {
                foreach (var (key, value) in incoming.Recipes)
                    mergedRecipes.Recipes[key] = value;
            }

            // Merge prisoner feeding entries.
            if (incoming.PrisonerFeeding != null)
            {
                foreach (var (key, value) in incoming.PrisonerFeeding)
                    mergedFeeding.PrisonerFeeding[key] = value;
            }
        }

        HeartLogger.Info(LOG_SOURCE,
            $"Loaded {mergedRecipes.Recipes.Count} recipe entry(s) and " +
            $"{mergedFeeding.PrisonerFeeding.Count} prisoner feeding entry(s) " +
            $"from '{recipesDir}'.");

        return (mergedRecipes, mergedFeeding);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static IEnumerable<string> GetJsonFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            HeartLogger.Warning(LOG_SOURCE, $"Config directory not found: '{directory}'");
            return Enumerable.Empty<string>();
        }

        // Sort alphabetically — later files win on key collision, so sort order
        // determines override priority. Consistent across OS file systems.
        return Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories)
                        .OrderBy(f => f, StringComparer.Ordinal);
    }

    static T? Deserialize<T>(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, _readOptions);
        }
        catch (Exception ex)
        {
            HeartLogger.Warning(LOG_SOURCE, $"Failed to read '{path}': {ex.Message}");
            return default;
        }
    }
}

// ── Combined file wrapper ─────────────────────────────────────────────────────

/// <summary>
/// Wrapper that allows a single *.json file to contain both recipe overrides
/// and prisoner feeding config under separate top-level keys.
/// Admins can populate one or both blocks in each file.
///
/// Example file layout:
/// {
///   "Recipes": {
///     "Recipe_Weapon_Sword_T01_Bone": { "ChangesEnabled": true, ... }
///   },
///   "PrisonerFeeding": {
///     "Item_Food_Gruel": { "ChangesEnabled": true, "HealthChange": 15 }
///   }
/// }
/// </summary>
file class CookbookRecipeFile
{
    public Dictionary<string, RecipeEntryData>?      Recipes         { get; set; }
    public Dictionary<string, PrisonerFeedEntryData>? PrisonerFeeding { get; set; }
}