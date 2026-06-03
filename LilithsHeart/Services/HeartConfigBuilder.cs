// ============================================================
//  HeartConfigBuilder — LilithsHeart
//  LilithsHeart/Services/HeartConfigBuilder.cs
//
//  Coordinates all config file generation for the suite.
//  Called from Heart.OnInitialize() before ItemService loads.
//
//  Three generation paths:
//  ────────────────────────
//  GenerateHeartExamples (HeartConfig flag):
//    Writes Items/ItemExamples.json with Heart's own appearance
//    fields only (DisplayName, DescriptionText, Icon).
//    Always overwrites.
//
//  GenerateAllModuleExamples (HeartConfig flag):
//    Merges Heart's appearance examples with all registered module
//    item example contributions into Items/ItemExamples.json.
//    Then calls each registered module example generator so
//    module-specific files are also written.
//    Always overwrites. Takes priority over GenerateHeartExamples
//    if both flags are set on the same boot.
//
//  GenerateDebugConfigs (HeartConfig flag):
//    Calls all registered module debug generators.
//    Always overwrites.
//
//  Module registration:
//  ─────────────────────
//  Modules call these in Load() before Heart initializes:
//    RegisterItemExamples(label, entries)   — item example data to
//      merge into Items/ItemExamples.json
//    RegisterExampleGenerator(Action)       — module's own example
//      file generator, called by GenerateAllModuleExamples
//    RegisterDebugGenerator(Action)         — module's own debug
//      file generator, called by GenerateDebugConfigs
//
//  [CHANGED] Full overhaul. Old RegisterGenerator() replaced by
//            three typed registration methods. GenerateIfRequested()
//            replaced by three separate generation methods called
//            from Heart.OnInitialize() based on which flags are set.
//            GenerateAllModuleExamples takes priority over
//            GenerateHeartExamples when both are set.
//
//  [PERFORMANCE] Zero cost on normal boots — all work gated behind
//                config flag checks.
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

    // Item example contributions from registered modules.
    // Each entry is (moduleLabel, LilithItemData dictionary).
    static readonly List<(string Label, Dictionary<string, LilithItemData> Entries)>
        _itemExampleContributors = [];

    // Module example file generators — called by GenerateAllModuleExamples.
    static readonly List<Action> _exampleGenerators = [];

    // Module debug file generators — called by GenerateDebugConfigs.
    static readonly List<Action> _debugGenerators = [];

    static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Registration API ─────────────────────────────────────

    /// <summary>
    /// Registers item example entries to be merged into
    /// Items/ItemExamples.json when GenerateAllModuleExamples fires.
    /// Call from module Load() before Heart initializes.
    ///
    /// label   — module name shown as a comment block in output
    /// entries — LilithItemData keyed by prefab name; set only
    ///           the fields your module owns, leave others null
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
    /// Registers a module's example file generator.
    /// Called by GenerateAllModuleExamples after writing ItemExamples.json.
    /// Call from module Load() before Heart initializes.
    /// </summary>
    public static void RegisterExampleGenerator(Action generator)
    {
        if (generator != null)
            _exampleGenerators.Add(generator);
    }

    /// <summary>
    /// Registers a module's debug file generator.
    /// Called by GenerateDebugConfigs.
    /// Call from module Load() before Heart initializes.
    /// </summary>
    public static void RegisterDebugGenerator(Action generator)
    {
        if (generator != null)
            _debugGenerators.Add(generator);
    }

    // ── Generation entry points ───────────────────────────────

    /// <summary>
    /// Checks all generation flags and runs the appropriate generators.
    /// Called from Heart.OnInitialize() before ItemService.
    ///
    /// GenerateAllModuleExamples takes priority over GenerateHeartExamples
    /// when both are set on the same boot — only one ItemExamples.json
    /// write occurs (the full merged one).
    /// </summary>
    public static void RunIfRequested()
    {
        // GenerateAllModuleExamples takes priority.
        if (HeartConfig.GenerateAllModuleExamples)
        {
            GenerateAllModuleExamples();
            HeartConfig.DisableGenerateAllModuleExamples();

            // Suppress HeartExamples on same boot — already covered.
            if (HeartConfig.GenerateHeartExamples)
                HeartConfig.DisableGenerateHeartExamples();
        }
        else if (HeartConfig.GenerateHeartExamples)
        {
            GenerateHeartItemExamples();
            HeartConfig.DisableGenerateHeartExamples();
        }

        if (HeartConfig.GenerateDebugConfigs)
        {
            GenerateDebugConfigs();
            HeartConfig.DisableGenerateDebugConfigs();
        }

        // Name alias generation is handled separately by PrefabNameResolver
        // after it initializes — HeartConfig.GenerateNameAliasConfigs is
        // checked there, not here.
    }

    // ── Heart item examples (appearance only) ─────────────────

    /// <summary>
    /// Writes Items/ItemExamples.json with Heart's own appearance
    /// field examples only (DisplayName, DescriptionText, Icon).
    /// Always overwrites.
    /// </summary>
    public static void GenerateHeartItemExamples()
    {
        var path = Path.Combine(HeartPathIndex.ItemsDir, "ItemExamples.json");
        Directory.CreateDirectory(HeartPathIndex.ItemsDir);

        var entries = BuildHeartAppearanceExamples();
        WriteItemExamples(path, entries, "LilithsHeart",
            "Appearance fields — DisplayName, DescriptionText, Icon. " +
            "Always applied when non-null regardless of ChangesEnabled.");

        HeartLogger.Info(LOG_SOURCE, "Generated Items/ItemExamples.json (Heart appearance only).");
    }

    // ── All module examples (merged) ──────────────────────────

    /// <summary>
    /// Merges Heart's appearance examples with all registered module
    /// item contributions into Items/ItemExamples.json, then calls
    /// each registered module example generator.
    /// Always overwrites.
    /// </summary>
    static void GenerateAllModuleExamples()
    {
        var path = Path.Combine(HeartPathIndex.ItemsDir, "ItemExamples.json");
        Directory.CreateDirectory(HeartPathIndex.ItemsDir);

        // Start with Heart's appearance examples.
        var merged = BuildHeartAppearanceExamples();

        // Merge each module's item contributions.
        foreach (var (label, entries) in _itemExampleContributors)
        {
            foreach (var (key, incoming) in entries)
            {
                if (!merged.TryGetValue(key, out var existing))
                {
                    merged[key] = incoming;
                    continue;
                }

                // Per-field merge — module adds its fields to existing entry.
                if (incoming.DisplayName     is not null) existing.DisplayName     = incoming.DisplayName;
                if (incoming.DescriptionText is not null) existing.DescriptionText = incoming.DescriptionText;
                if (incoming.Icon            is not null) existing.Icon            = incoming.Icon;
                if (incoming.StackSize.HasValue)          existing.StackSize       = incoming.StackSize;
                // Promote ChangesEnabled if any contributor enables it.
                if (incoming.ChangesEnabled)              existing.ChangesEnabled  = true;
            }
        }

        string moduleList = _itemExampleContributors.Count > 0
            ? string.Join(", ", _itemExampleContributors.Select(c => c.Label))
            : "none";

        WriteItemExamples(path, merged,
            $"LilithsHeart + {moduleList}",
            "All item override fields from all installed modules. " +
            "Appearance fields always apply when non-null. " +
            "ChangesEnabled gates functional fields (StackSize, etc.).");

        HeartLogger.Info(LOG_SOURCE,
            $"Generated Items/ItemExamples.json " +
            $"(merged: Heart + {_itemExampleContributors.Count} module contributor(s)).");

        // Call each module's own example generator.
        foreach (var generator in _exampleGenerators)
        {
            try { generator(); }
            catch (Exception ex)
            {
                HeartLogger.Error(LOG_SOURCE, $"Example generator failed: {ex.Message}");
            }
        }
    }

    // ── Debug configs ─────────────────────────────────────────

    /// <summary>
    /// Calls all registered module debug generators.
    /// Always overwrites.
    /// </summary>
    static void GenerateDebugConfigs()
    {
        if (_debugGenerators.Count == 0)
        {
            HeartLogger.Info(LOG_SOURCE, "No debug generators registered — nothing to generate.");
            return;
        }

        HeartLogger.Info(LOG_SOURCE,
            $"GenerateDebugConfigs — running {_debugGenerators.Count} debug generator(s).");

        foreach (var generator in _debugGenerators)
        {
            try { generator(); }
            catch (Exception ex)
            {
                HeartLogger.Error(LOG_SOURCE, $"Debug generator failed: {ex.Message}");
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────

    /// <summary>
    /// Builds Heart's built-in appearance example entries.
    /// Three items demonstrating the three icon resolution methods
    /// plus display name and description overrides.
    /// ChangesEnabled = false (appearance fields have no gate,
    /// but we set false on the functional field gate so the
    /// example doesn't accidentally enable stack size changes).
    /// </summary>
    static Dictionary<string, LilithItemData> BuildHeartAppearanceExamples()
        => new(StringComparer.Ordinal)
        {
            ["Item_BloodEssence_T01"] = new LilithItemData
            {
                DisplayName     = "Vitae",
                DescriptionText = "Concentrated life force, harvested from the living.",
                Icon            = "vitae.png",
                ChangesEnabled  = false,
            },
            ["Item_Ingredient_Gem_Ruby_T01"] = new LilithItemData
            {
                DisplayName    = "Bloodstone",
                Icon           = "Icon_BloodOrb",
                ChangesEnabled = false,
            },
            ["Item_MagicSource_BloodKey_T01"] = new LilithItemData
            {
                DisplayName    = "Crimson Key",
                Icon           = "https://example.com/icons/crimson-key.png",
                ChangesEnabled = false,
            },
        };

    /// <summary>
    /// Serializes item example entries to JSON with readme headers.
    /// Always overwrites the target file.
    /// </summary>
    static void WriteItemExamples(
        string path,
        Dictionary<string, LilithItemData> entries,
        string source,
        string fieldNote)
    {
        try
        {
            // Build the output object with readme entries first.
            var output = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["_readme"] =
                    "Keys are prefab Name or Prefab string from LilithsMind PrefabDef entries " +
                    "(e.g. 'BloodEssence' or 'Item_BloodEssence_T01'). " +
                    "Files load alphabetically — later files win per-field on key conflicts.",
                ["_fields"] = fieldNote,
                ["_source"] = source,
                ["_icon_readme"] =
                    "Icon: (1) PNG filename in client Icons/ folder e.g. 'vitae.png'; " +
                    "(2) in-game sprite name e.g. 'Icon_BloodOrb'; " +
                    "(3) https:// URL downloaded and cached by client.",
                ["_changesEnabled_readme"] =
                    "ChangesEnabled gates functional fields (StackSize). " +
                    "Appearance fields (DisplayName, DescriptionText, Icon) " +
                    "always apply when non-null regardless of ChangesEnabled.",
            };

            foreach (var (key, data) in entries)
                output[key] = data;

            var json = JsonSerializer.Serialize(output, _writeOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            HeartLogger.Warning(LOG_SOURCE, $"Could not write '{path}': {ex.Message}");
        }
    }
}