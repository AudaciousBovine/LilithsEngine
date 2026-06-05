// ============================================================
//  CookbookConfigBuilder — LilithsCookbook
//  LilithsCookbook/Services/CookbookConfigBuilder.cs
//
//  Generates all Cookbook config files from embedded JSON resources.
//
//  [CHANGED] All Write*() methods replaced with embedded resource
//            extractions. JSON files live in LilithsCookbook/Resources/
//            and are compiled into the DLL as EmbeddedResource.
//            Generation code is now trivial — resource → file copy.
//
//  Example files (ChangesEnabled = false):
//    Recipes/Examples_Recipe.json
//    Recipes/Examples_PrisonerFeed.json
//    Recipes/Examples_PrisonerFed.json
//    Items/Examples_CookbookItem.json
//
//  Debug files (ChangesEnabled = true, values visibly changed):
//    Recipes/Debug_Recipe.json
//    Recipes/Debug_PrisonerFeed.json
//    Recipes/Debug_PrisonerFed.json
//    Items/Debug_CookbookItem.json
//
//  Reference dumps (ECS data, on-demand):
//    Recipes/AllRecipes.json
//
//  [PERFORMANCE] All generation runs once per flag trigger.
//                Resource extraction is O(file size) — negligible.
// ============================================================

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectM;
using Unity.Entities;
using LilithsMind.Data;
using LilithsHeart.Config;
using LilithsHeart.Foundation;
using LilithsHeart.Services;
using LilithsCookbook.Config;
using LilithsCookbook.Data;

namespace LilithsCookbook.Services;

public static class CookbookConfigBuilder
{
    private const string LOG_SOURCE    = "LilithsCookbook.CookbookConfigBuilder";
    private const string ASSEMBLY_NAME = "LilithsCookbook";

    public static readonly string RecipesDir = HeartPathIndex.DataDir("Recipes");

    // ── Output file paths ─────────────────────────────────────
    static readonly string AllRecipesPath          = Path.Combine(RecipesDir, "AllRecipes.json");
    static readonly string RecipeExamplesPath      = Path.Combine(RecipesDir, "Examples_Recipe.json");
    static readonly string PrisonerFeedExamplesPath = Path.Combine(RecipesDir, "Examples_PrisonerFeed.json");
    static readonly string PrisonerFedExamplesPath  = Path.Combine(RecipesDir, "Examples_PrisonerFed.json");
    static readonly string RecipeDebugPath          = Path.Combine(RecipesDir, "Debug_Recipe.json");
    static readonly string PrisonerFeedDebugPath    = Path.Combine(RecipesDir, "Debug_PrisonerFeed.json");
    static readonly string PrisonerFedDebugPath     = Path.Combine(RecipesDir, "Debug_PrisonerFed.json");
    static readonly string CookbookItemExamplesPath = Path.Combine(HeartPathIndex.ItemsDir, "Examples_CookbookItem.json");
    static readonly string CookbookItemDebugPath    = Path.Combine(HeartPathIndex.ItemsDir, "Debug_CookbookItem.json");

    static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Initialization ────────────────────────────────────────

    /// <summary>
    /// Creates config directories. Example/debug files are written
    /// on demand via config flags — not on first run.
    /// Call from CookbookPlugin.Load() before Heart is ready.
    /// </summary>
    public static void Initialize()
    {
        Directory.CreateDirectory(RecipesDir);
        Directory.CreateDirectory(HeartPathIndex.ItemsDir);
    }

    // ── Public generation entry points ────────────────────────

    /// <summary>
    /// Extracts all four Cookbook example files from embedded resources.
    /// Always overwrites. Called when GenerateCookbookExamples = true
    /// OR when Heart's GenerateAllModuleExamples triggers it.
    /// </summary>
    public static void GenerateExampleFiles()
    {
        HeartLogger.Info(LOG_SOURCE, "Generating Cookbook example files...");
        Extract("Examples.Examples_Recipe.json",      RecipeExamplesPath);
        Extract("Examples.Examples_PrisonerFeed.json", PrisonerFeedExamplesPath);
        Extract("Examples.Examples_PrisonerFed.json",  PrisonerFedExamplesPath);
        Extract("Examples.Examples_CookbookItem.json", CookbookItemExamplesPath);
        HeartLogger.Info(LOG_SOURCE, "Cookbook example files generated.");
    }

    /// <summary>
    /// Extracts all four Cookbook debug files from embedded resources.
    /// Always overwrites. Called when GenerateCookbookDebugConfigs = true
    /// OR when Heart's GenerateDebugConfigs triggers it.
    /// </summary>
    public static void GenerateDebugFiles()
    {
        HeartLogger.Info(LOG_SOURCE, "Generating Cookbook debug files...");
        Extract("Debug.Debug_Recipe.json",          RecipeDebugPath);
        Extract("Debug.Debug_PrisonerFeed.json",    PrisonerFeedDebugPath);
        Extract("Debug.Debug_PrisonerFed.json",     PrisonerFedDebugPath);
        Extract("Debug.Debug_CookbookItem.json",    CookbookItemDebugPath);
        HeartLogger.Info(LOG_SOURCE, "Cookbook debug files generated.");
    }

    /// <summary>
    /// Dumps all vanilla recipes from ECS to AllRecipes.json.
    /// Called when GenerateAllRecipes = true in CookbookConfig.
    /// Requires Heart to be initialized (ECS access needed).
    /// Always overwrites. Resets flag after generation.
    /// </summary>
    public static void GenerateAllRecipesIfRequested()
    {
        if (!CookbookConfig.GenerateAllRecipes) return;

        HeartLogger.Info(LOG_SOURCE, "GenerateAllRecipes enabled — generating AllRecipes.json...");

        try
        {
            var recipeMap = Heart.GameDataSystem.RecipeHashLookupMap;
            var entries   = new Dictionary<string, RecipeEntryData>(recipeMap.Count());
            var prefabMap = Heart.PrefabCollectionSystem._PrefabGuidToEntityMap;

            foreach (var kvp in recipeMap)
            {
                var recipeData = kvp.Value;

                if (!PrefabNameResolver.TryResolveName(kvp.Key, out string recipeName))
                    recipeName = kvp.Key.GuidHash.ToString();

                var entry = new RecipeEntryData
                {
                    ChangesEnabled       = false,
                    CraftDuration        = recipeData.CraftDuration,
                    AlwaysUnlocked       = recipeData.AlwaysUnlocked,
                    HideInStation        = recipeData.HideInStation,
                    IgnoreServerSettings = recipeData.IgnoreServerSettings,
                    HudSortingOrder      = recipeData.HudSortingOrder,
                    Stations             = null,
                };

                if (!prefabMap.TryGetValue(kvp.Key, out Entity entity))
                {
                    entries[recipeName] = entry;
                    continue;
                }

                if (entity.TryGetBuffer<RecipeRequirementBuffer>(out var reqBuffer) && reqBuffer.Length > 0)
                {
                    entry.Requirements = new List<CookbookItemData>(reqBuffer.Length);
                    for (int i = 0; i < reqBuffer.Length; i++)
                    {
                        var req = reqBuffer[i];
                        PrefabNameResolver.TryResolveName(req.Guid, out string itemName);
                        entry.Requirements.Add(new CookbookItemData
                        {
                            Item   = string.IsNullOrEmpty(itemName) ? req.Guid._Value.ToString() : itemName,
                            Amount = req.Amount,
                        });
                    }
                }

                if (entity.TryGetBuffer<RecipeOutputBuffer>(out var outBuffer) && outBuffer.Length > 0)
                {
                    entry.Outputs = new List<CookbookItemData>(outBuffer.Length);
                    for (int i = 0; i < outBuffer.Length; i++)
                    {
                        var output = outBuffer[i];
                        PrefabNameResolver.TryResolveName(output.Guid, out string itemName);
                        entry.Outputs.Add(new CookbookItemData
                        {
                            Item   = string.IsNullOrEmpty(itemName) ? output.Guid._Value.ToString() : itemName,
                            Amount = output.Amount,
                        });
                    }
                }

                if (entity.TryGetBuffer<ItemRepairBuffer>(out var repairBuffer) && repairBuffer.Length > 0)
                {
                    entry.UseRepairCosts = true;
                    entry.RepairCosts    = new List<CookbookItemData>(repairBuffer.Length);
                    for (int i = 0; i < repairBuffer.Length; i++)
                    {
                        var cost = repairBuffer[i];
                        PrefabNameResolver.TryResolveName(cost.Guid, out string itemName);
                        entry.RepairCosts.Add(new CookbookItemData
                        {
                            Item   = string.IsNullOrEmpty(itemName) ? cost.Guid._Value.ToString() : itemName,
                            Amount = cost.Stacks,
                        });
                    }
                }

                if (entity.TryGetBuffer<RecipeOutputUnitBuffer>(out var unitBuffer) && unitBuffer.Length > 0)
                {
                    entry.UseUnitOutputs = true;
                    entry.UnitOutputs    = new List<CookbookItemData>(unitBuffer.Length);
                    for (int i = 0; i < unitBuffer.Length; i++)
                    {
                        var unit = unitBuffer[i];
                        PrefabNameResolver.TryResolveName(unit.Guid, out string unitName);
                        entry.UnitOutputs.Add(new CookbookItemData
                        {
                            Item   = string.IsNullOrEmpty(unitName) ? unit.Guid._Value.ToString() : unitName,
                            Amount = unit.Stacks,
                        });
                    }
                }

                if (entity.TryGetBuffer<RecipeLinkBuffer>(out var linkBuffer) && linkBuffer.Length > 0)
                {
                    entry.UseRecipeLinks = true;
                    entry.RecipeLinks    = new List<string>(linkBuffer.Length);
                    for (int i = 0; i < linkBuffer.Length; i++)
                    {
                        var link = linkBuffer[i];
                        PrefabNameResolver.TryResolveName(link.Guid, out string linkName);
                        entry.RecipeLinks.Add(
                            string.IsNullOrEmpty(linkName) ? link.Guid._Value.ToString() : linkName);
                    }
                }

                entries[recipeName] = entry;
            }

            var file = new CookbookRecipeFile
            {
                Recipes         = entries,
                PrisonerFeeding = new Dictionary<string, PrisonerFeedEntryData>(),
            };

            WriteJson(AllRecipesPath, file);
            HeartLogger.Info(LOG_SOURCE, $"AllRecipes.json written with {entries.Count} entries.");
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE, $"Failed to generate AllRecipes.json: {ex.Message}");
        }
        finally
        {
            CookbookConfig.DisableGenerateAllRecipes();
        }
    }

    // ── Helpers ───────────────────────────────────────────────

    /// <summary>
    /// Extracts an embedded JSON resource to the given output path.
    /// [CHANGED] Delegates to HeartConfigBuilder.ExtractResource using
    ///           the Cookbook assembly name.
    /// </summary>
    static void Extract(string fileName, string outputPath)
    {
        var resourceName = $"{ASSEMBLY_NAME}.Resources.{fileName}";
        var assembly     = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            HeartLogger.Error(LOG_SOURCE,
                $"Embedded resource '{resourceName}' not found. " +
                "Ensure the file is marked as EmbeddedResource in LilithsCookbook.csproj.");
            return;
        }

        using var reader = new StreamReader(stream);
        File.WriteAllText(outputPath, reader.ReadToEnd());
        HeartLogger.Debug(LOG_SOURCE,
            $"Extracted '{fileName}' → '{Path.GetFileName(outputPath)}'.");
    }

    static void WriteJson<T>(string path, T data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, _writeOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE, $"Failed to write '{Path.GetFileName(path)}': {ex.Message}");
        }
    }
}

// ── Combined file wrapper ─────────────────────────────────────────────────────
file class CookbookRecipeFile
{
    public Dictionary<string, RecipeEntryData>?       Recipes         { get; set; }
    public Dictionary<string, PrisonerFeedEntryData>? PrisonerFeeding { get; set; }
}