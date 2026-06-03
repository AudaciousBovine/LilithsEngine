// ============================================================
//  CookbookConfigBuilder — LilithsCookbook
//  LilithsCookbook/Systems/CookbookConfigBuilder.cs
//
//  Generates all Cookbook config files — examples, debug, and
//  the vanilla recipe dump.
//
//  [CHANGED] Full overhaul to match the new suite-wide config
//            generation system. Old one-off flags replaced by:
//              GenerateCookbookExamples   → GenerateExampleFiles()
//              GenerateCookbookDebugConfigs → GenerateDebugFiles()
//            Both are always-overwrite. No file-exists checks.
//
//  Example files (ChangesEnabled = false):
//    Recipes/RecipeExamples.json        — 3 recipe entries
//    Recipes/PrisonerFeedExamples.json  — feed recipe entries
//    Recipes/PrisonerFedExamples.json   — 3 FakeItem entries
//    Items/CookbookItemExamples.json    — 3 stack size entries
//
//  Debug files (ChangesEnabled = true, values visibly changed):
//    Recipes/RecipeDebug.json
//    Recipes/PrisonerFeedDebug.json
//    Recipes/PrisonerFedDebug.json
//    Items/CookbookItemDebug.json
//
//  Reference dumps (ECS data, on-demand):
//    Recipes/AllRecipes.json            — vanilla recipe ECS dump
//
//  [CHANGED] Initialize() no longer writes example files on first
//            run — examples are written on demand via config flags.
//            Only creates the Recipes/ directory.
//
//  [PERFORMANCE] All generation runs once per flag trigger.
//                No per-frame cost.
// ============================================================

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
    private const string LOG_SOURCE = "LilithsCookbook.CookbookConfigBuilder";

    public static readonly string RecipesDir = HeartPathIndex.DataDir("Recipes");

    // ── File paths ────────────────────────────────────────────
    static readonly string AllRecipesPath           = Path.Combine(RecipesDir, "AllRecipes.json");
    static readonly string RecipeExamplesPath        = Path.Combine(RecipesDir, "RecipeExamples.json");
    static readonly string PrisonerFeedExamplesPath  = Path.Combine(RecipesDir, "PrisonerFeedExamples.json");
    static readonly string PrisonerFedExamplesPath   = Path.Combine(RecipesDir, "PrisonerFedExamples.json");
    static readonly string RecipeDebugPath           = Path.Combine(RecipesDir, "RecipeDebug.json");
    static readonly string PrisonerFeedDebugPath     = Path.Combine(RecipesDir, "PrisonerFeedDebug.json");
    static readonly string PrisonerFedDebugPath      = Path.Combine(RecipesDir, "PrisonerFedDebug.json");
    static readonly string CookbookItemExamplesPath  = Path.Combine(HeartPathIndex.ItemsDir, "CookbookItemExamples.json");
    static readonly string CookbookItemDebugPath     = Path.Combine(HeartPathIndex.ItemsDir, "CookbookItemDebug.json");

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
    /// Generates all four Cookbook example files.
    /// Always overwrites. Called when GenerateCookbookExamples = true
    /// OR when Heart's GenerateAllModuleExamples triggers it.
    /// </summary>
    public static void GenerateExampleFiles()
    {
        HeartLogger.Info(LOG_SOURCE, "Generating Cookbook example files...");
        WriteRecipeExamples();
        WritePrisonerFeedExamples();
        WritePrisonerFedExamples();
        WriteCookbookItemExamples();
        HeartLogger.Info(LOG_SOURCE, "Cookbook example files generated.");
    }

    /// <summary>
    /// Generates all four Cookbook debug files.
    /// Always overwrites. Called when GenerateCookbookDebugConfigs = true
    /// OR when Heart's GenerateDebugConfigs triggers it.
    /// </summary>
    public static void GenerateDebugFiles()
    {
        HeartLogger.Info(LOG_SOURCE, "Generating Cookbook debug files...");
        WriteRecipeDebug();
        WritePrisonerFeedDebug();
        WritePrisonerFedDebug();
        WriteCookbookItemDebug();
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

    // ── Example writers ───────────────────────────────────────

    static void WriteRecipeExamples()
    {
        var file = new CookbookRecipeFile
        {
            Recipes = new Dictionary<string, RecipeEntryData>
            {
                // Example 1 — change requirements and craft duration
                ["Recipe_Weapon_Sword_T01_Bone"] = new RecipeEntryData
                {
                    ChangesEnabled = false,
                    CraftDuration  = 10f,
                    Requirements   = new List<CookbookItemData>
                    {
                        new() { Item = "Item_Ingredient_Bone", Amount = 8 },
                        new() { Item = "Item_BloodEssence_T01", Amount = 1 },
                    },
                },
                // Example 2 — move a recipe to different stations
                ["Recipe_Weapon_Sword_T04_Copper_Reinforced"] = new RecipeEntryData
                {
                    ChangesEnabled = false,
                    Stations       = new List<string> { "Blacksmith", "MobileBlacksmith" },
                },
                // Example 3 — hide a recipe and change outputs
                ["Recipe_Consumable_WranglerSprayCan"] = new RecipeEntryData
                {
                    ChangesEnabled = false,
                    HideInStation  = true,
                    Outputs        = new List<CookbookItemData>
                    {
                        new() { Item = "Item_Consumable_WranglerSprayCan", Amount = 5 },
                    },
                },
            },
        };

        WriteJson(RecipeExamplesPath, file);
        HeartLogger.Info(LOG_SOURCE, "Written RecipeExamples.json.");
    }

    static void WritePrisonerFeedExamples()
    {
        // PrisonerFeedExamples covers the Recipe side of prisoner feeding —
        // i.e. which real item triggers a feed action and what it outputs.
        // These are standard RecipeData entries handled by RecipeSystem.
        var file = new CookbookRecipeFile
        {
            Recipes = new Dictionary<string, RecipeEntryData>
            {
                // Example 1 — change the food item required for SageFish feed
                ["Recipe_Misc_FeedPrisoner_Fish_SageFish"] = new RecipeEntryData
                {
                    ChangesEnabled = false,
                    CraftDuration  = 30f,
                    Requirements   = new List<CookbookItemData>
                    {
                        new() { Item = "Item_Ingredient_Fish_SageFish_T02", Amount = 1 },
                    },
                    Outputs = new List<CookbookItemData>
                    {
                        new() { Item = "FakeItem_FeedPrisoner_SageFish", Amount = 1 },
                    },
                },
                // Example 2 — feed recipe with a real item output alongside the FakeItem
                // This is how blood extraction works — FakeItem + real reward item
                ["Recipe_Misc_ExtractEssencePrisoner"] = new RecipeEntryData
                {
                    ChangesEnabled = false,
                    CraftDuration  = 4f,
                    Outputs        = new List<CookbookItemData>
                    {
                        new() { Item = "FakeItem_Prisoner_ExtractEssence", Amount = 1 },
                        new() { Item = "Item_BloodEssence_T01", Amount = 30 },
                    },
                },
            },
        };

        WriteJson(PrisonerFeedExamplesPath, file);
        HeartLogger.Info(LOG_SOURCE, "Written PrisonerFeedExamples.json.");
    }

    static void WritePrisonerFedExamples()
    {
        // PrisonerFedExamples covers the FakeItem side — the stat effects
        // that happen to the prisoner when the feed recipe completes.
        var file = new CookbookRecipeFile
        {
            PrisonerFeeding = new Dictionary<string, PrisonerFeedEntryData>
            {
                // Example 1 — FeedPrisoner (standard food, health + misery recovery)
                ["FakeItem_FeedPrisoner_SageFish"] = new PrisonerFeedEntryData
                {
                    ChangesEnabled    = false,
                    Type              = PrisonerFeedTypeEnum.FeedPrisoner,
                    RecoverHealth_Min = 0.3f,
                    RecoverHealth_Max = 0.7f,
                    RecoverMisery_Min = 0.1f,
                    RecoverMisery_Max = 0.2f,
                    AlterBloodQuality_Min = 0.0f,
                    AlterBloodQuality_Max = 0.0f,
                },
                // Example 2 — DealDamageToPrisoner (blood extraction, damages prisoner)
                ["FakeItem_Prisoner_ExtractEssence"] = new PrisonerFeedEntryData
                {
                    ChangesEnabled             = false,
                    Type                       = PrisonerFeedTypeEnum.DealDamageToPrisoner,
                    DealPercentualDamage_Min   = 0.1f,
                    DealPercentualDamage_Max   = 0.3f,
                    DealPercentualTorture_Min  = 0.02f,
                    DealPercentualTorture_Max  = 0.06f,
                },
                // Example 3 — AffectWithToxic (irradiant food, mutation chance)
                ["FakeItem_FeedPrisoner_IrradiantGruel"] = new PrisonerFeedEntryData
                {
                    ChangesEnabled           = false,
                    Type                     = PrisonerFeedTypeEnum.AffectWithToxic,
                    ChanceToBecomeMutant     = 0.35f,
                    IncreaseBloodQuality_Min = 0.01f,
                    IncreaseBloodQuality_Max = 0.02f,
                },
            },
        };

        WriteJson(PrisonerFedExamplesPath, file);
        HeartLogger.Info(LOG_SOURCE, "Written PrisonerFedExamples.json.");
    }

    static void WriteCookbookItemExamples()
    {
        // CookbookItemExamples covers functional item overrides owned by Cookbook.
        // Lives in Items/ so ItemService picks it up alongside appearance overrides.
        var entries = new Dictionary<string, LilithItemData>
        {
            // Example 1 — increase stack size of a resource
            ["Item_BloodEssence_T01"] = new LilithItemData
            {
                ChangesEnabled = false,
                StackSize      = 500,
            },
            // Example 2 — increase stack size of a crafting ingredient
            ["Item_Ingredient_Mineral_CopperOre"] = new LilithItemData
            {
                ChangesEnabled = false,
                StackSize      = 1000,
            },
            // Example 3 — increase stack size of a consumable
            ["Item_Consumable_Salve_Vermin"] = new LilithItemData
            {
                ChangesEnabled = false,
                StackSize      = 50,
            },
        };

        WriteJson(CookbookItemExamplesPath, entries);
        HeartLogger.Info(LOG_SOURCE, "Written Items/CookbookItemExamples.json.");
    }

    // ── Debug writers ─────────────────────────────────────────

    static void WriteRecipeDebug()
    {
        var file = new CookbookRecipeFile
        {
            Recipes = new Dictionary<string, RecipeEntryData>
            {
                // Debug: cut craft duration to 1 second — immediately obvious in-game
                ["Recipe_Weapon_Sword_T01_Bone"] = new RecipeEntryData
                {
                    ChangesEnabled = true,
                    CraftDuration  = 1f,
                },
                // Debug: make a recipe always unlocked
                ["Recipe_Weapon_Sword_T04_Copper_Reinforced"] = new RecipeEntryData
                {
                    ChangesEnabled = true,
                    AlwaysUnlocked = true,
                    CraftDuration  = 1f,
                },
                // Debug: move a recipe to a different station
                ["Recipe_Consumable_WranglerSprayCan"] = new RecipeEntryData
                {
                    ChangesEnabled = true,
                    Stations       = new List<string> { "Blacksmith" },
                },
            },
        };

        WriteJson(RecipeDebugPath, file);
        HeartLogger.Info(LOG_SOURCE, "Written RecipeDebug.json.");
    }

    static void WritePrisonerFeedDebug()
    {
        var file = new CookbookRecipeFile
        {
            Recipes = new Dictionary<string, RecipeEntryData>
            {
                // Debug: cut all feed recipe durations to 1 second
                ["Recipe_Misc_FeedPrisoner_Fish_SageFish"] = new RecipeEntryData
                {
                    ChangesEnabled = true,
                    CraftDuration  = 1f,
                },
                ["Recipe_Misc_FeedPrisoner_IrradiantGruel"] = new RecipeEntryData
                {
                    ChangesEnabled = true,
                    CraftDuration  = 1f,
                },
                ["Recipe_Misc_ExtractEssencePrisoner"] = new RecipeEntryData
                {
                    ChangesEnabled = true,
                    CraftDuration  = 1f,
                },
            },
        };

        WriteJson(PrisonerFeedDebugPath, file);
        HeartLogger.Info(LOG_SOURCE, "Written PrisonerFeedDebug.json.");
    }

    static void WritePrisonerFedDebug()
    {
        var file = new CookbookRecipeFile
        {
            PrisonerFeeding = new Dictionary<string, PrisonerFeedEntryData>
            {
                // Debug: near-full health restore on SageFish — very obvious in-game
                ["FakeItem_FeedPrisoner_SageFish"] = new PrisonerFeedEntryData
                {
                    ChangesEnabled    = true,
                    Type              = PrisonerFeedTypeEnum.FeedPrisoner,
                    RecoverHealth_Min = 0.95f,
                    RecoverHealth_Max = 0.99f,
                    RecoverMisery_Min = 0.0f,
                    RecoverMisery_Max = 0.0f,
                    AlterBloodQuality_Min = 0.01f,
                    AlterBloodQuality_Max = 0.01f,
                },
                // Debug: zero damage extraction — extraction never hurts prisoner
                ["FakeItem_Prisoner_ExtractEssence"] = new PrisonerFeedEntryData
                {
                    ChangesEnabled            = true,
                    Type                      = PrisonerFeedTypeEnum.DealDamageToPrisoner,
                    DealPercentualDamage_Min  = 0.01f,
                    DealPercentualDamage_Max  = 0.01f,
                    DealPercentualTorture_Min = 0.0f,
                    DealPercentualTorture_Max = 0.0f,
                },
                // Debug: zero mutation chance on gruel — feed repeatedly, no mutations
                ["FakeItem_FeedPrisoner_IrradiantGruel"] = new PrisonerFeedEntryData
                {
                    ChangesEnabled           = true,
                    Type                     = PrisonerFeedTypeEnum.AffectWithToxic,
                    ChanceToBecomeMutant     = 0.0f,
                    IncreaseBloodQuality_Min = 0.04f,
                    IncreaseBloodQuality_Max = 0.08f,
                },
            },
        };

        WriteJson(PrisonerFedDebugPath, file);
        HeartLogger.Info(LOG_SOURCE, "Written PrisonerFedDebug.json.");
    }

    static void WriteCookbookItemDebug()
    {
        var entries = new Dictionary<string, LilithItemData>
        {
            // Debug: obviously large stack sizes — immediately visible in inventory
            ["Item_BloodEssence_T01"] = new LilithItemData
            {
                ChangesEnabled = true,
                StackSize      = 9999,
            },
            ["Item_Ingredient_Mineral_CopperOre"] = new LilithItemData
            {
                ChangesEnabled = true,
                StackSize      = 9999,
            },
            ["Item_Consumable_Salve_Vermin"] = new LilithItemData
            {
                ChangesEnabled = true,
                StackSize      = 999,
            },
        };

        WriteJson(CookbookItemDebugPath, entries);
        HeartLogger.Info(LOG_SOURCE, "Written Items/CookbookItemDebug.json.");
    }

    // ── Helpers ───────────────────────────────────────────────

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