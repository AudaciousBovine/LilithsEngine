using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using LilithsHeart.Foundation;
using LilithsHeart.Services;
using LilithsMind.Data;
using LilithsCookbook.Data;

namespace LilithsCookbook.Systems;

// ============================================================
//  RecipeSystem — LilithsCookbook
//
//  Applies recipe changes from recipes.json to server-side ECS
//  prefab entities and RecipeHashLookupMap, then registers
//  overrides with Heart for Soul client sync.
//
//  Why RecipeHashLookupMap must be written directly:
//  ──────────────────────────────────────────────────
//  The map is populated from baked scene data at startup and is
//  NOT updated by RegisterRecipes() from live entity components.
//  The crafting system reads CraftDuration and other scalar fields
//  from the map — not the entity — so both must be kept in sync.
//
//  [CHANGED] BuildSoulOverride() builds Requirements and Outputs
//            as Dictionary<string, int> (prefab name → amount)
//            matching the simplified LilithRecipeData structure.
//
//  [CHANGED] RecipeRequirement, RecipeOutput, RecipeRepairCost,
//            RecipeUnitOutput consolidated into CookbookItemData.
//            ECS buffer types (RecipeRequirementBuffer etc.) are
//            unchanged — those are V Rising game types, not ours.
//
//  [CHANGED] RecipeEntry → RecipeEntryData to follow naming convention.
//
//  [PERFORMANCE] ApplyChanges() runs once at startup. All ECS
//                writes and map updates are one-time costs.
// ============================================================
public static class RecipeSystem
{
    private const string LOG_SOURCE = "LilithsCookbook.RecipeSystem";

    public static void ApplyChanges()
    {
        var config = CookbookPlugin.RecipeData;

        if (config == null || config.Recipes.Count == 0)
        {
            HeartLogger.Info(LOG_SOURCE, "No recipe changes configured.");
            return;
        }

        int changed = 0;

        // [PERFORMANCE] Dict pre-sized to config count — avoids
        // rehashing during the apply loop.
        var soulOverrides = new Dictionary<string, LilithRecipeData>(config.Recipes.Count);

        foreach (var (recipeName, entry) in config.Recipes)
        {
            if (!entry.ChangesEnabled) continue;

            if (!PrefabNameResolver.TryResolve(recipeName, out PrefabGUID guid))
            {
                HeartLogger.Warning(LOG_SOURCE, $"Could not resolve recipe: '{recipeName}'");
                continue;
            }

            if (!Heart.PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(guid, out Entity recipeEntity))
            {
                HeartLogger.Warning(LOG_SOURCE, $"Could not find prefab entity for recipe: '{recipeName}'");
                continue;
            }

            if (entry.CraftDuration.HasValue       ||
                entry.AlwaysUnlocked.HasValue       ||
                entry.HideInStation.HasValue        ||
                entry.IgnoreServerSettings.HasValue ||
                entry.HudSortingOrder.HasValue)
            {
                ApplyRecipeData(recipeEntity, entry, guid);
            }

            if (entry.Requirements != null)
                ApplyRequirements(recipeEntity, entry.Requirements, recipeName);

            if (entry.Outputs != null)
                ApplyOutputs(recipeEntity, entry.Outputs, recipeName);

            if (entry.UseRepairCosts.HasValue)
                ApplyOptionalBuffer(recipeEntity, entry.UseRepairCosts.Value, entry.RepairCosts, recipeName,
                    ApplyRepairCosts);

            if (entry.UseUnitOutputs.HasValue)
                ApplyOptionalBuffer(recipeEntity, entry.UseUnitOutputs.Value, entry.UnitOutputs, recipeName,
                    ApplyUnitOutputs);

            if (entry.UseRecipeLinks.HasValue)
                ApplyOptionalBuffer(recipeEntity, entry.UseRecipeLinks.Value, entry.RecipeLinks, recipeName,
                    ApplyRecipeLinks);

            changed++;

            soulOverrides[recipeName] = BuildSoulOverride(recipeEntity);
        }

        if (changed > 0)
        {
            Heart.GameDataSystem.RegisterRecipes();
            HeartLogger.Info(LOG_SOURCE, $"LilithsCookbook applied changes to {changed} recipe(s).");

            Heart.RegisterRecipeOverrides(soulOverrides);
            HeartLogger.Info(LOG_SOURCE,
                $"Registered {soulOverrides.Count} recipe override(s) with Heart for Soul sync.");
        }
        else
        {
            HeartLogger.Info(LOG_SOURCE, "No recipes had ChangesEnabled = true, skipping registration.");
        }
    }

    // ── Soul override builder ─────────────────────────────────

    /// <summary>
    /// Builds a LilithRecipeData by reading the current ECS entity state
    /// after all changes have been applied. Reading from ECS rather than the
    /// config entry ensures the override reflects what was actually committed.
    ///
    /// [PERFORMANCE] Called once per changed recipe at startup only.
    /// </summary>
    static LilithRecipeData BuildSoulOverride(Entity recipeEntity)
    {
        var result = new LilithRecipeData();

        if (recipeEntity.TryGetComponent<RecipeData>(out var recipeData))
            result.CraftDuration = recipeData.CraftDuration;

        // Build Requirements as Dictionary<string, int>.
        // RecipeRequirementBuffer is a V Rising ECS type — not renamed.
        if (recipeEntity.TryGetBuffer<RecipeRequirementBuffer>(out var reqBuffer))
        {
            result.Requirements = new Dictionary<string, int>(reqBuffer.Length);
            for (int i = 0; i < reqBuffer.Length; i++)
            {
                var req = reqBuffer[i];
                PrefabNameResolver.TryResolveName(req.Guid, out string itemName);
                var key = string.IsNullOrEmpty(itemName)
                    ? req.Guid._Value.ToString()
                    : itemName;
                result.Requirements[key] = req.Amount;
            }
        }

        // Build Outputs as Dictionary<string, int>.
        // RecipeOutputBuffer is a V Rising ECS type — not renamed.
        if (recipeEntity.TryGetBuffer<RecipeOutputBuffer>(out var outBuffer))
        {
            result.Outputs = new Dictionary<string, int>(outBuffer.Length);
            for (int i = 0; i < outBuffer.Length; i++)
            {
                var output = outBuffer[i];
                PrefabNameResolver.TryResolveName(output.Guid, out string itemName);
                var key = string.IsNullOrEmpty(itemName)
                    ? output.Guid._Value.ToString()
                    : itemName;
                result.Outputs[key] = output.Amount;
            }
        }

        return result;
    }

    // ── Per-field apply ───────────────────────────────────────

    /// <summary>
    /// Applies scalar RecipeData fields to both the prefab entity component
    /// and directly into RecipeHashLookupMap.
    /// [PERFORMANCE] One map read + one map write per changed recipe at startup only.
    /// </summary>
    static void ApplyRecipeData(Entity recipeEntity, RecipeEntryData entry, PrefabGUID guid)
    {
        var data = recipeEntity.Read<RecipeData>();

        if (entry.CraftDuration.HasValue)       data.CraftDuration        = entry.CraftDuration.Value;
        if (entry.AlwaysUnlocked.HasValue)       data.AlwaysUnlocked       = entry.AlwaysUnlocked.Value;
        if (entry.HideInStation.HasValue)        data.HideInStation        = entry.HideInStation.Value;
        if (entry.IgnoreServerSettings.HasValue) data.IgnoreServerSettings = entry.IgnoreServerSettings.Value;
        if (entry.HudSortingOrder.HasValue)      data.HudSortingOrder      = entry.HudSortingOrder.Value;

        recipeEntity.Write(data);

        var map = Heart.GameDataSystem.RecipeHashLookupMap;
        if (map.TryGetValue(guid, out var mapEntry))
        {
            if (entry.CraftDuration.HasValue)       mapEntry.CraftDuration        = entry.CraftDuration.Value;
            if (entry.AlwaysUnlocked.HasValue)       mapEntry.AlwaysUnlocked       = entry.AlwaysUnlocked.Value;
            if (entry.HideInStation.HasValue)        mapEntry.HideInStation        = entry.HideInStation.Value;
            if (entry.IgnoreServerSettings.HasValue) mapEntry.IgnoreServerSettings = entry.IgnoreServerSettings.Value;
            if (entry.HudSortingOrder.HasValue)      mapEntry.HudSortingOrder      = entry.HudSortingOrder.Value;
            map[guid] = mapEntry;

            HeartLogger.Debug(LOG_SOURCE,
                $"Updated RecipeHashLookupMap for '{guid._Value}': " +
                $"CraftDuration={mapEntry.CraftDuration}");
        }
        else
        {
            HeartLogger.Warning(LOG_SOURCE,
                $"Recipe GUID {guid._Value} not found in RecipeHashLookupMap — scalar fields may not apply.");
        }
    }

    // [CHANGED] Parameter type: List<CookbookItemData> — our config DTO.
    //           Buffer type: RecipeRequirementBuffer — V Rising ECS type, unchanged.
    static void ApplyRequirements(Entity recipeEntity, List<CookbookItemData> requirements, string recipeName)
    {
        if (!recipeEntity.TryGetBuffer<RecipeRequirementBuffer>(out var buffer))
            buffer = recipeEntity.AddBuffer<RecipeRequirementBuffer>();

        buffer.Clear();

        foreach (var req in requirements)
        {
            if (!PrefabNameResolver.TryResolve(req.Item, out PrefabGUID itemGuid))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"[{recipeName}] Could not resolve requirement item: '{req.Item}', skipping.");
                continue;
            }

            buffer.Add(new RecipeRequirementBuffer { Guid = itemGuid, Amount = req.Amount });
        }
    }

    // [CHANGED] Parameter type: List<CookbookItemData> — our config DTO.
    //           Buffer type: RecipeOutputBuffer — V Rising ECS type, unchanged.
    static void ApplyOutputs(Entity recipeEntity, List<CookbookItemData> outputs, string recipeName)
    {
        if (!recipeEntity.TryGetBuffer<RecipeOutputBuffer>(out var buffer))
            buffer = recipeEntity.AddBuffer<RecipeOutputBuffer>();

        buffer.Clear();

        foreach (var output in outputs)
        {
            if (!PrefabNameResolver.TryResolve(output.Item, out PrefabGUID itemGuid))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"[{recipeName}] Could not resolve output item: '{output.Item}', skipping.");
                continue;
            }

            buffer.Add(new RecipeOutputBuffer { Guid = itemGuid, Amount = output.Amount });
        }
    }

    static void ApplyOptionalBuffer<T>(
        Entity recipeEntity,
        bool enabled,
        T? list,
        string recipeName,
        Action<Entity, T, string> applyAction)
        where T : class
    {
        if (!enabled)
        {
            RemoveBuffer<T>(recipeEntity, recipeName);
            return;
        }

        if (list == null)
        {
            HeartLogger.Warning(LOG_SOURCE,
                $"[{recipeName}] Flag set to true but list is null, skipping.");
            return;
        }

        applyAction(recipeEntity, list, recipeName);
    }

    // [CHANGED] Type checks updated to List<CookbookItemData> for repair costs
    //           and unit outputs. ECS buffer types are V Rising types — unchanged.
    static void RemoveBuffer<T>(Entity recipeEntity, string recipeName) where T : class
    {
        if (typeof(T) == typeof(List<CookbookItemData>))
        {
            // Ambiguous — could be repair costs or unit outputs.
            // Check which buffer exists and remove it.
            if (recipeEntity.Has<ItemRepairBuffer>())
            {
                recipeEntity.Remove<ItemRepairBuffer>();
                HeartLogger.Info(LOG_SOURCE, $"[{recipeName}] Removed ItemRepairBuffer.");
            }
            else if (recipeEntity.Has<RecipeOutputUnitBuffer>())
            {
                recipeEntity.Remove<RecipeOutputUnitBuffer>();
                HeartLogger.Info(LOG_SOURCE, $"[{recipeName}] Removed RecipeOutputUnitBuffer.");
            }
            else
            {
                HeartLogger.Info(LOG_SOURCE, $"[{recipeName}] Buffer already absent, nothing to remove.");
            }
        }
        else if (typeof(T) == typeof(List<string>))
        {
            if (recipeEntity.Has<RecipeLinkBuffer>())
            {
                recipeEntity.Remove<RecipeLinkBuffer>();
                HeartLogger.Info(LOG_SOURCE, $"[{recipeName}] Removed RecipeLinkBuffer.");
            }
            else
            {
                HeartLogger.Info(LOG_SOURCE, $"[{recipeName}] RecipeLinkBuffer already absent, nothing to remove.");
            }
        }
    }

    // [CHANGED] Parameter type: List<CookbookItemData> — consolidated from RecipeRepairCost.
    //           Buffer type: ItemRepairBuffer — V Rising ECS type, unchanged.
    static void ApplyRepairCosts(Entity recipeEntity, List<CookbookItemData> repairCosts, string recipeName)
    {
        if (!recipeEntity.TryGetBuffer<ItemRepairBuffer>(out var buffer))
            buffer = recipeEntity.AddBuffer<ItemRepairBuffer>();

        buffer.Clear();

        foreach (var cost in repairCosts)
        {
            if (!PrefabNameResolver.TryResolve(cost.Item, out PrefabGUID itemGuid))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"[{recipeName}] Could not resolve repair cost item: '{cost.Item}', skipping.");
                continue;
            }

            buffer.Add(new ItemRepairBuffer { Guid = itemGuid, Stacks = cost.Amount });
        }
    }

    // [CHANGED] Parameter type: List<CookbookItemData> — consolidated from RecipeUnitOutput.
    //           'Unit' field renamed to 'Item' in CookbookItemData.
    //           Buffer type: RecipeOutputUnitBuffer — V Rising ECS type, unchanged.
    static void ApplyUnitOutputs(Entity recipeEntity, List<CookbookItemData> unitOutputs, string recipeName)
    {
        if (!recipeEntity.TryGetBuffer<RecipeOutputUnitBuffer>(out var buffer))
            buffer = recipeEntity.AddBuffer<RecipeOutputUnitBuffer>();

        buffer.Clear();

        foreach (var unit in unitOutputs)
        {
            if (!PrefabNameResolver.TryResolve(unit.Item, out PrefabGUID unitGuid))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"[{recipeName}] Could not resolve unit output: '{unit.Item}', skipping.");
                continue;
            }

            buffer.Add(new RecipeOutputUnitBuffer { Guid = unitGuid, Stacks = unit.Amount });
        }
    }

    static void ApplyRecipeLinks(Entity recipeEntity, List<string> recipeLinks, string recipeName)
    {
        if (!recipeEntity.TryGetBuffer<RecipeLinkBuffer>(out var buffer))
            buffer = recipeEntity.AddBuffer<RecipeLinkBuffer>();

        buffer.Clear();

        foreach (var linkName in recipeLinks)
        {
            if (!PrefabNameResolver.TryResolve(linkName, out PrefabGUID linkGuid))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"[{recipeName}] Could not resolve recipe link: '{linkName}', skipping.");
                continue;
            }

            buffer.Add(new RecipeLinkBuffer { Guid = linkGuid });
        }
    }
}