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
//  [CHANGED] JsonStringEnumConverter moved from global _readOptions
//            to a [JsonConverter] attribute on PrisonerFeedEntryData.Type
//            directly (in CookbookPrisonerFeedData.cs).
//            A global JsonStringEnumConverter in System.Text.Json on
//            .NET 6 can silently null out nullable value-type fields
//            (float?, bool?, int?) during deserialization of complex
//            objects — causing CraftDuration, AlwaysUnlocked etc. to
//            always deserialize as null despite being present in JSON.
//            Per-field attribution scopes the converter correctly.
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

    // [CHANGED] No JsonStringEnumConverter here — moved to a [JsonConverter]
    // attribute on PrisonerFeedEntryData.Type to avoid silently nulling out
    // nullable value-type fields across the whole object graph.
    static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
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
            var incoming = Deserialize<CookbookRecipeFile>(file);
            if (incoming == null) continue;

            if (incoming.Recipes != null)
            {
                foreach (var (key, value) in incoming.Recipes)
                    mergedRecipes.Recipes[key] = value;
            }

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
/// </summary>
file class CookbookRecipeFile
{
    public Dictionary<string, RecipeEntryData>?       Recipes         { get; set; }
    public Dictionary<string, PrisonerFeedEntryData>? PrisonerFeeding { get; set; }
}