// ============================================================
//  HeartConfigBuilder — LilithsHeart
//  LilithsHeart/Services/HeartConfigBuilder.cs
//
//  Generates example config files for all installed modules.
//  Called from Heart.OnInitialize() before ItemService loads,
//  so fresh examples are immediately picked up.
//
//  Two registration systems:
//  ──────────────────────────
//  1. _generators — arbitrary Action callbacks for generating
//     non-item config files (recipes, stations, etc.)
//     Modules call RegisterGenerator(Action) in Load().
//
//  2. _itemExampleContributors — typed item example data from
//     each installed module. Modules call RegisterItemExamples()
//     with a label and a dictionary of LilithItemData entries.
//     Heart's GenerateItemsExample() merges ALL contributors
//     into one Items/example.json so admins see every available
//     field in one file regardless of which modules are installed.
//
//  [CHANGED] RegisterItemExamples() added. GenerateItemsExample()
//            now merges Heart's built-in examples with all
//            registered contributor entries. Each contributor's
//            entries are grouped under a comment block showing
//            which module contributed them.
//
//  [PERFORMANCE] Zero cost on normal boots — all work gated behind
//                the GenerateExampleConfigs flag check.
// ============================================================

using System.Text.Json;
using System.Text.Json.Serialization;
using LilithsMind.Data;
using LilithsHeart.Config;
using LilithsHeart.Foundation;

namespace LilithsHeart.Services;

public static class HeartConfigBuilder
{
    private const string LOG_SOURCE = "LilithsHeart.HeartConfigBuilder";

    // Registered arbitrary generators (recipes, stations, etc.)
    static readonly List<Action> _generators = [];

    // [CHANGED] Registered item example contributors.
    // Each entry is a (moduleLabel, entries) tuple.
    // Merged into Items/example.json by GenerateItemsExample().
    static readonly List<(string Label, Dictionary<string, LilithItemData> Entries)>
        _itemExampleContributors = [];

    static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Registers an arbitrary example file generator.
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
    /// Registers item example entries to be merged into Items/example.json.
    /// Call this from your module's Load() before Heart initializes.
    ///
    /// [CHANGED] New registration path — replaces per-module example file
    ///           writing. All item examples are merged into one file so
    ///           admins see every available field regardless of which
    ///           modules are installed.
    ///
    /// Parameters:
    ///   label   — module name shown as a comment block in the output,
    ///             e.g. "LilithsCookbook" or "LilithsArmory"
    ///   entries — item overrides to include as examples, keyed by
    ///             prefab name. Set fields your module owns;
    ///             leave others null.
    /// </summary>
    public static void RegisterItemExamples(
        string label,
        Dictionary<string, LilithItemData> entries)
    {
        if (string.IsNullOrWhiteSpace(label) || entries == null || entries.Count == 0)
            return;

        _itemExampleContributors.Add((label, entries));
        HeartLogger.Debug(LOG_SOURCE,
            $"Registered {entries.Count} item example(s) from '{label}'.");
    }

    /// <summary>
    /// Checks HeartConfig.GenerateExampleConfigs and runs all
    /// registered generators plus GenerateItemsExample() if true.
    /// Resets the flag to false after all generators complete.
    /// Called by Heart.OnInitialize() before ItemService.
    /// </summary>
    public static void GenerateIfRequested()
    {
        if (!HeartConfig.GenerateExampleConfigs) return;

        HeartLogger.Info(LOG_SOURCE,
            $"GenerateExampleConfigs is true — running {_generators.Count + 1} generator(s).");

        // Always regenerate the items example (delete + rewrite) so it
        // reflects whatever modules are currently installed.
        GenerateItemsExample();

        foreach (var generator in _generators)
        {
            try { generator(); }
            catch (Exception ex)
            {
                HeartLogger.Error(LOG_SOURCE, $"Generator failed: {ex.Message}");
            }
        }

        HeartConfig.DisableGenerateExampleConfigs();
    }

    // ── Built-in generators ───────────────────────────────────

    /// <summary>
    /// Generates Items/example.json merging Heart's built-in examples
    /// with all registered module contributors.
    ///
    /// [CHANGED] Now merges all RegisterItemExamples() contributors
    ///           into one file. Always overwrites so it stays current
    ///           as modules are added or removed.
    ///           Each contributor's entries are preceded by a
    ///           _source comment identifying the module.
    /// </summary>
    public static void GenerateItemsExample()
    {
        var itemsDir    = HeartPathIndex.ItemsDir;
        var examplePath = Path.Combine(itemsDir, "example.json");

        try
        {
            Directory.CreateDirectory(itemsDir);

            // Build the merged entry set.
            // Use an ordered dictionary (insertion order preserved in .NET)
            // so Heart's entries come first, then each module in registration order.
            var merged = new Dictionary<string, object>(StringComparer.Ordinal);

            // ── Readme entries ────────────────────────────────────────────────
            merged["_readme"] =
                "Keys are the prefab Name or Prefab string from LilithsMind PrefabDef entries " +
                "(e.g. 'BloodEssence' or 'Item_BloodEssence_T01'). All fields are optional — " +
                "omit any you do not want to change. Files in subdirectories are included " +
                "automatically. Files load in full-path alphabetical order — later files win " +
                "per-field on key conflicts.";

            merged["_icon_readme"] =
                "Icon can be set three ways: (1) a PNG filename in the client's Icons/ folder " +
                "e.g. 'vitae.png'; (2) an in-game sprite name e.g. 'Icon_BloodOrb'; " +
                "(3) an https:// URL the client will download and cache.";

            merged["_description_readme"] =
                "DescriptionText sets the item tooltip body. Two items that share a vanilla " +
                "description stay independent — each gets its own localization key.";

            // ── Heart built-in appearance examples ────────────────────────────
            merged["_source_heart"] = "LilithsHeart — appearance fields (DisplayName, DescriptionText, Icon)";

            merged["Item_BloodEssence_T01"] = new LilithItemData
            {
                DisplayName     = "Vitae",
                DescriptionText = "Concentrated life force, harvested from the living.",
                Icon            = "vitae.png",
            };

            merged["Item_Ingredient_Gem_Ruby_T01"] = new LilithItemData
            {
                DisplayName = "Bloodstone",
                Icon        = "Icon_BloodOrb",
            };

            merged["Item_MagicSource_BloodKey_T01"] = new LilithItemData
            {
                DisplayName = "Crimson Key",
                Icon        = "https://example.com/icons/crimson-key.png",
            };

            // ── Registered module contributors ────────────────────────────────
            foreach (var (label, entries) in _itemExampleContributors)
            {
                merged[$"_source_{label.ToLowerInvariant().Replace(" ", "_")}"] =
                    $"{label} — module-specific fields";

                foreach (var (key, data) in entries)
                {
                    // Merge with any existing entry for this key so Heart's
                    // appearance fields and a module's functional fields can
                    // coexist on the same item in the example output.
                    if (merged.TryGetValue(key, out var existing) &&
                        existing is LilithItemData existingData)
                    {
                        if (data.DisplayName     is not null) existingData.DisplayName     = data.DisplayName;
                        if (data.DescriptionText is not null) existingData.DescriptionText = data.DescriptionText;
                        if (data.Icon            is not null) existingData.Icon            = data.Icon;
                        if (data.StackSize.HasValue)          existingData.StackSize       = data.StackSize;
                    }
                    else
                    {
                        merged[key] = data;
                    }
                }
            }

            var json = JsonSerializer.Serialize(merged, _writeOptions);
            File.WriteAllText(examplePath, json);

            int moduleCount = _itemExampleContributors.Count;
            HeartLogger.Info(LOG_SOURCE,
                $"Generated Items/example.json — Heart + {moduleCount} module contributor(s).");
        }
        catch (Exception ex)
        {
            HeartLogger.Warning(LOG_SOURCE,
                $"Could not write Items example: {ex.Message}");
        }
    }
}