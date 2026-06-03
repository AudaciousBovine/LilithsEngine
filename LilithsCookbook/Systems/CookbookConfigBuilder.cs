// ============================================================
//  CookbookConfigBuilder — LilithsCookbook
//  LilithsCookbook/Systems/CookbookConfigBuilder.cs
//
//  Generates JSON config files from ECS data and writes
//  example files for admins.
//
//  [CHANGED] WriteExampleStations() removed entirely.
//            CookbookStationData is retired — station membership
//            is now declared inline on each recipe's Stations list.
//            The Stations/ directory is no longer created.
//
//  [CHANGED] WriteExampleRecipes() updated to demonstrate the
//            Stations list field on a recipe entry, and to include
//            a PrisonerFeeding example block alongside Recipes in
//            the same file. PrisonerFeeding keys are FakeItem prefab
//            names (not food item names) with a Type discriminator
//            matching the ECS component on each FakeItem prefab.
//            ECS components confirmed from assembly via dnSpy:
//              FeedPrisoner, AffectPrisonerWithToxic, DealDamageToPrisoner.
//
//  [CHANGED] GenerateAllRecipesIfRequested() now writes Stations: null
//            on all dumped entries (vanilla state = no station
//            changes declared). Admins fill it in manually.
//
//  [CHANGED] GeneratePrisonerFeedExampleIfRequested() added.
//            Writes prisoner-feed-example.json with ChangesEnabled=true
//            entries for all three FakeItem behaviour types plus their
//            feed recipes. Values differ visibly from vanilla for easy
//            in-game verification. Auto-resets flag after write.
//
//  [PERFORMANCE] Example file generation and GenerateAllRecipes
//                both run at most once at startup, gated by config
//                flags. No per-frame cost.
// ============================================================

using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectM;
using Unity.Entities;
using LilithsHeart.Config;
using LilithsHeart.Foundation;
using LilithsHeart.Services;
using LilithsCookbook.Config;
using LilithsCookbook.Data;

namespace LilithsCookbook.Systems;

public static class CookbookConfigBuilder
{
    private const string LOG_SOURCE = "LilithsCookbook.CookbookConfigBuilder";

    public static readonly string RecipesDir = HeartPathIndex.DataDir("Recipes");

    // [CHANGED] StationsDir removed — no longer created or used.

    static readonly string ExampleRecipesPath        = Path.Combine(RecipesDir, "example-recipes.json");
    static readonly string AllRecipesPath             = Path.Combine(RecipesDir, "all-recipes.json");
    // [CHANGED] Prisoner feed example path — written on demand via config flag.
    static readonly string PrisonerFeedExamplePath    = Path.Combine(RecipesDir, "prisoner-feed-example.json");

    static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ── Initialization (no ECS — safe to call from Plugin.Load) ──────────────

    /// <summary>
    /// Creates config directories and writes example files if they don't exist.
    /// Call this from CookbookPlugin.Load() before Heart is ready.
    ///
    /// [CHANGED] No longer creates Stations/ directory or example-stations.json.
    /// </summary>
    public static void Initialize()
    {
        Directory.CreateDirectory(RecipesDir);

        if (!File.Exists(ExampleRecipesPath))
            WriteExampleRecipes();
    }

    // ── ECS generation (call after Heart.OnInitialized) ──────────────────────

    /// <summary>
    /// If GeneratePrisonerFeedExample is enabled in CookbookConfig, writes
    /// prisoner-feed-example.json with ChangesEnabled=true entries covering all
    /// three FakeItem behaviour types and their corresponding feed recipes.
    ///
    /// Values are set visibly different from vanilla so the effect is immediately
    /// obvious in-game — e.g. SageFish heals 99% health, IrradiantGruel has 0%
    /// mutation chance, ExtractEssence deals minimal damage.
    ///
    /// Called from CookbookPlugin.OnHeartInitialized() before LoadRecipes() so
    /// the generated file is picked up on the same boot if desired.
    ///
    /// [CHANGED] Added alongside GenerateAllRecipesIfRequested().
    ///           Same auto-reset pattern via CookbookConfig.DisableGeneratePrisonerFeedExample().
    ///
    /// [PERFORMANCE] Runs once when flag is set. Pure JSON write — no ECS access needed.
    ///               Resets flag automatically after write.
    /// </summary>
    public static void GeneratePrisonerFeedExampleIfRequested()
    {
        if (!CookbookConfig.GeneratePrisonerFeedExample) return;

        HeartLogger.Info(LOG_SOURCE,
            "GeneratePrisonerFeedExample is enabled — writing prisoner-feed-example.json...");

        try
        {
            // Each entry has ChangesEnabled = true and values visibly changed from vanilla
            // so you can immediately confirm in-game whether the system is applying changes.
            //
            // Recipe entries: change CraftDuration to something obvious (1 second).
            // FakeItem entries: change stat values to extreme/zero so the effect is clear.
            //
            // ── FeedPrisoner archetype ────────────────────────────────────────────────
            // Vanilla SageFish: Health=[0.3–0.7], Misery=[0.1–0.2], BloodQuality=[0–0]
            // Test values: max health recovery (0.99), zero misery recovery (0.0),
            //              slight blood quality gain — obviously different in-game.
            //
            // ── AffectWithToxic archetype ─────────────────────────────────────────────
            // Vanilla IrradiantGruel: MutantChance=0.35, BloodQuality=[0.01–0.02]
            // Test values: zero mutation chance (easy to verify — feed many times,
            //              no mutations), doubled blood quality gain.
            //
            // ── DealDamageToPrisoner archetype ────────────────────────────────────────
            // Vanilla ExtractEssence: Damage=[0.1–0.3], Torture=[0.02–0.06]
            // Test values: minimal damage (0.01–0.01 flat), zero torture —
            //              extraction should barely hurt the prisoner.

            var file = new CookbookRecipeFile
            {
                Recipes = new Dictionary<string, RecipeEntryData>
                {
                    // FeedPrisoner recipe — SageFish feed
                    // Changed: CraftDuration 30s → 1s (instant, easy to test)
                    ["Recipe_Misc_FeedPrisoner_Fish_SageFish"] = new RecipeEntryData
                    {
                        ChangesEnabled = true,
                        CraftDuration  = 1f,
                    },

                    // AffectWithToxic recipe — IrradiantGruel feed
                    // Changed: CraftDuration 3s → 1s
                    ["Recipe_Misc_FeedPrisoner_IrradiantGruel"] = new RecipeEntryData
                    {
                        ChangesEnabled = true,
                        CraftDuration  = 1f,
                    },

                    // DealDamageToPrisoner recipe — ExtractEssence
                    // Changed: CraftDuration 4s → 1s
                    ["Recipe_Misc_ExtractEssencePrisoner"] = new RecipeEntryData
                    {
                        ChangesEnabled = true,
                        CraftDuration  = 1f,
                    },
                },

                PrisonerFeeding = new Dictionary<string, PrisonerFeedEntryData>
                {
                    // ── FeedPrisoner — FakeItem_FeedPrisoner_SageFish ─────────────────
                    // Test: near-full health restore, no misery change, tiny blood quality
                    // In-game signal: prisoner health jumps to near-max after feeding.
                    ["FakeItem_FeedPrisoner_SageFish"] = new PrisonerFeedEntryData
                    {
                        ChangesEnabled        = true,
                        Type                  = PrisonerFeedTypeEnum.FeedPrisoner,
                        RecoverHealth_Min     = 0.95f,
                        RecoverHealth_Max     = 0.99f,
                        RecoverMisery_Min     = 0.0f,
                        RecoverMisery_Max     = 0.0f,
                        AlterBloodQuality_Min = 0.01f,
                        AlterBloodQuality_Max = 0.01f,
                    },

                    // ── AffectWithToxic — FakeItem_FeedPrisoner_IrradiantGruel ────────
                    // Test: zero mutation chance, doubled blood quality gain
                    // In-game signal: gruel never triggers mutation no matter how many
                    //                 times fed; blood quality increases faster than vanilla.
                    ["FakeItem_FeedPrisoner_IrradiantGruel"] = new PrisonerFeedEntryData
                    {
                        ChangesEnabled           = true,
                        Type                     = PrisonerFeedTypeEnum.AffectWithToxic,
                        ChanceToBecomeMutant     = 0.0f,
                        IncreaseBloodQuality_Min = 0.04f,
                        IncreaseBloodQuality_Max = 0.08f,
                    },

                    // ── DealDamageToPrisoner — FakeItem_Prisoner_ExtractEssence ───────
                    // Test: minimal damage (0.01 flat), zero torture
                    // In-game signal: extracting essence barely damages the prisoner
                    //                 and does not increase their misery at all.
                    ["FakeItem_Prisoner_ExtractEssence"] = new PrisonerFeedEntryData
                    {
                        ChangesEnabled            = true,
                        Type                      = PrisonerFeedTypeEnum.DealDamageToPrisoner,
                        DealPercentualDamage_Min  = 0.01f,
                        DealPercentualDamage_Max  = 0.01f,
                        DealPercentualTorture_Min = 0.0f,
                        DealPercentualTorture_Max = 0.0f,
                    },
                },
            };

            WriteJson(PrisonerFeedExamplePath, file);
            HeartLogger.Info(LOG_SOURCE,
                $"prisoner-feed-example.json written to '{PrisonerFeedExamplePath}'. " +
                "Set GeneratePrisonerFeedExample=false or delete the file after testing.");
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE,
                $"Failed to write prisoner-feed-example.json: {ex.Message}");
        }
        finally
        {
            CookbookConfig.DisableGeneratePrisonerFeedExample();
        }
    }

    /// <summary>
    /// If GenerateAllRecipes is enabled in CookbookConfig, iterates all entries in
    /// GameDataSystem.RecipeHashLookupMap, serializes their current vanilla state to
    /// all-recipes.json, then disables the setting so it does not run on next boot.
    ///
    /// Useful for admins building their own recipe overrides — run once, then edit.
    ///
    /// [CHANGED] Stations field is written as null on every entry (no vanilla
    ///           station membership data is included in the dump — admins add
    ///           station declarations manually to the entries they want to move).
    ///
    /// [PERFORMANCE] Runs once when the flag is set. Iterates the full recipe map
    ///               and prefab entity map — O(recipes). Disk write at the end.
    ///               Auto-disables after one run.
    /// </summary>
    public static void GenerateAllRecipesIfRequested()
    {
        if (!CookbookConfig.GenerateAllRecipes) return;

        HeartLogger.Info(LOG_SOURCE, "GenerateAllRecipes is enabled — generating all-recipes.json...");

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
                    ChangesEnabled     = false,
                    CraftDuration      = recipeData.CraftDuration,
                    AlwaysUnlocked     = recipeData.AlwaysUnlocked,
                    HideInStation      = recipeData.HideInStation,
                    IgnoreServerSettings = recipeData.IgnoreServerSettings,
                    HudSortingOrder    = recipeData.HudSortingOrder,
                    // [CHANGED] Stations is intentionally null in the dump.
                    //           Admins declare station membership manually.
                    Stations           = null,
                };

                if (!prefabMap.TryGetValue(kvp.Key, out Entity entity))
                {
                    entries[recipeName] = entry;
                    continue;
                }

                // RecipeRequirementBuffer — V Rising ECS type, not renamed.
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
                            Amount = req.Amount
                        });
                    }
                }

                // RecipeOutputBuffer — V Rising ECS type, not renamed.
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
                            Amount = output.Amount
                        });
                    }
                }

                // ItemRepairBuffer — V Rising ECS type, not renamed.
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
                            Amount = cost.Stacks  // ItemRepairBuffer uses Stacks, not Amount
                        });
                    }
                }

                // RecipeOutputUnitBuffer — V Rising ECS type, not renamed.
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
                            Amount = unit.Stacks
                        });
                    }
                }

                // RecipeLinkBuffer — V Rising ECS type, not renamed.
                if (entity.TryGetBuffer<RecipeLinkBuffer>(out var linkBuffer) && linkBuffer.Length > 0)
                {
                    entry.UseRecipeLinks = true;
                    entry.RecipeLinks    = new List<string>(linkBuffer.Length);
                    for (int i = 0; i < linkBuffer.Length; i++)
                    {
                        var link = linkBuffer[i];
                        PrefabNameResolver.TryResolveName(link.Guid, out string linkName);
                        entry.RecipeLinks.Add(
                            string.IsNullOrEmpty(linkName) ? link.Guid._Value.ToString() : linkName
                        );
                    }
                }

                entries[recipeName] = entry;
            }

            // Write as a combined file so admins can add PrisonerFeeding blocks
            // to the same file if they want everything in one place.
            var file = new CookbookRecipeFile
            {
                Recipes         = entries,
                PrisonerFeeding = new Dictionary<string, PrisonerFeedEntryData>()
            };

            WriteJson(AllRecipesPath, file);
            HeartLogger.Info(LOG_SOURCE, $"all-recipes.json written with {entries.Count} entries.");
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE, $"Failed to generate all-recipes.json: {ex.Message}");
        }
        finally
        {
            CookbookConfig.DisableGenerateAllRecipes();
        }
    }

    // ── Example file writer ───────────────────────────────────────────────────

    /// <summary>
    /// Writes example-recipes.json demonstrating the recipe + station +
    /// prisoner feeding config format. Written once if the file doesn't exist.
    ///
    /// [CHANGED] Now uses the combined CookbookRecipeFile wrapper so admins
    ///           see both blocks (Recipes and PrisonerFeeding) in one example.
    ///           Stations list is shown inline on the recipe entry.
    ///           No separate example-stations.json is generated.
    /// </summary>
    static void WriteExampleRecipes()
    {
        var file = new CookbookRecipeFile
        {
            Recipes = new Dictionary<string, RecipeEntryData>
            {
                ["Recipe_Weapon_Sword_T01_Bone"] = new RecipeEntryData
                {
                    ChangesEnabled = false,
                    CraftDuration  = 10.0f,
                    Requirements   = new List<CookbookItemData>
                    {
                        new() { Item = "Item_Ingredient_Bone", Amount = 8 },
                        new() { Item = "Item_BloodEssence_T01", Amount = 1 }
                    },
                    // [CHANGED] Station membership declared inline.
                    // null = don't touch station membership for this recipe.
                    // []   = remove from all stations.
                    // [...] = explicit set of stations to appear in.
                    Stations = null
                },
                ["Recipe_Weapon_Sword_T04_Copper_Reinforced"] = new RecipeEntryData
                {
                    ChangesEnabled = false,
                    // Stations example: move this recipe to a different station.
                    Stations = new List<string> { "Blacksmith", "MobileBlacksmith" }
                }
            },
            // [CHANGED] PrisonerFeeding keys are FakeItem prefab names, not food item names.
            // Each FakeItem carries a specific ECS behaviour component — declare the
            // matching Type so PrisonerFeedSystem writes the correct component.
            // To change WHICH food item triggers a feed action, use the normal Recipes
            // block to modify the RecipeRequirementBuffer on the corresponding
            // Recipe_Misc_FeedPrisoner_* entry.
            PrisonerFeeding = new Dictionary<string, PrisonerFeedEntryData>
            {
                // Standard food — ProjectM.FeedPrisoner
                // Values are fractional (0.0–1.0) of the prisoner's max stat.
                ["FakeItem_FeedPrisoner_SageFish"] = new PrisonerFeedEntryData
                {
                    ChangesEnabled     = false,
                    Type               = PrisonerFeedTypeEnum.FeedPrisoner,
                    RecoverHealth_Min  = 0.3f,
                    RecoverHealth_Max  = 0.7f,
                    RecoverMisery_Min  = 0.1f,
                    RecoverMisery_Max  = 0.2f,
                    AlterBloodQuality_Min = 0.0f,
                    AlterBloodQuality_Max = 0.0f,
                },
                // Toxic/irradiant food — ProjectM.AffectPrisonerWithToxic
                ["FakeItem_FeedPrisoner_IrradiantGruel"] = new PrisonerFeedEntryData
                {
                    ChangesEnabled            = false,
                    Type                      = PrisonerFeedTypeEnum.AffectWithToxic,
                    ChanceToBecomeMutant      = 0.35f,
                    IncreaseBloodQuality_Min  = 0.01f,
                    IncreaseBloodQuality_Max  = 0.02f,
                },
                // Blood extraction — ProjectM.DealDamageToPrisoner
                // Values are fractional damage/misery dealt to the prisoner.
                ["FakeItem_Prisoner_ExtractedBloodPotion"] = new PrisonerFeedEntryData
                {
                    ChangesEnabled             = false,
                    Type                       = PrisonerFeedTypeEnum.DealDamageToPrisoner,
                    DealPercentualDamage_Min   = 0.3f,
                    DealPercentualDamage_Max   = 0.6f,
                    DealPercentualTorture_Min  = 0.1f,
                    DealPercentualTorture_Max  = 0.2f,
                },
            }
        };

        WriteJson(ExampleRecipesPath, file);
        HeartLogger.Info(LOG_SOURCE, "Generated example-recipes.json.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void WriteJson<T>(string path, T data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, _writeOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE, $"Failed to write '{path}': {ex.Message}");
        }
    }
}

// ── Combined file wrapper (internal to builder — matches loader's wrapper) ────

/// <summary>
/// Internal wrapper matching the CookbookLoader file shape.
/// Allows GenerateAllRecipesIfRequested() to write a file the loader
/// will recognize (with both Recipes and PrisonerFeeding top-level keys).
/// Declared here as file-scoped to avoid conflicts with the loader's
/// identical private wrapper.
/// </summary>
file class CookbookRecipeFile
{
    public Dictionary<string, RecipeEntryData>?       Recipes         { get; set; }
    public Dictionary<string, PrisonerFeedEntryData>? PrisonerFeeding { get; set; }
}