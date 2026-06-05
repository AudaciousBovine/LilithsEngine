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
//  Why RecipeHashLookupMap must be written LAST:
//  ──────────────────────────────────────────────────
//  Both RecipeSystem and StationSystem call RegisterRecipes()
//  (and StationSystem also calls RegisterGameData()). Each
//  RegisterRecipes() REBUILDS RecipeHashLookupMap from baked
//  scene data, wiping any scalar-field writes (CraftDuration,
//  AlwaysUnlocked, etc.) made before it.
//
//  The crafting COMPLETION system reads CraftDuration from the
//  map — not the entity. So if the map is reset after our write,
//  completion uses the vanilla duration (often 86400s = 24h for
//  recipes that are workstation-only in vanilla). The entity
//  component write still drives the initial countdown display,
//  which is why the timer LOOKS correct but completion fails.
//
//  [CHANGED] Map writes split into a separate ApplyMapValues()
//            method, called by CookbookPlugin AFTER StationSystem
//            has finished all its RegisterRecipes()/RegisterGameData()
//            calls. This guarantees the map scalar writes are the
//            final ECS mutation and are never reset.
//
//            ApplyChanges() now does:
//              • entity component writes (survive registration)
//              • buffer writes (requirements, outputs, etc.)
//              • RegisterRecipes()
//              • Soul override registration
//            ApplyMapValues() (called last, post-StationSystem) does:
//              • RecipeHashLookupMap scalar field writes only
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
//  [PERFORMANCE] ApplyChanges() and ApplyMapValues() each run once
//                at startup. All ECS writes are one-time costs.
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

            // Entity component writes — survive RegisterRecipes(), drive the
            // initial countdown display. Map writes happen later in ApplyMapValues().
            if (entry.CraftDuration.HasValue       ||
                entry.AlwaysUnlocked.HasValue       ||
                entry.HideInStation.HasValue        ||
                entry.IgnoreServerSettings.HasValue ||
                entry.HudSortingOrder.HasValue)
            {
                ApplyRecipeEntityOnly(recipeEntity, entry);
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

            soulOverrides[recipeName] = BuildSoulOverride(recipeEntity, entry);
        }

        if (changed == 0)
        {
            HeartLogger.Info(LOG_SOURCE, "No recipes had ChangesEnabled = true, skipping registration.");
            return;
        }

        Heart.GameDataSystem.RegisterRecipes();
        HeartLogger.Info(LOG_SOURCE, $"LilithsCookbook applied changes to {changed} recipe(s).");

        Heart.RegisterRecipeOverrides(soulOverrides);
        HeartLogger.Info(LOG_SOURCE,
            $"Registered {soulOverrides.Count} recipe override(s) with Heart for Soul sync.");
    }

    /// <summary>
    /// [CHANGED] Writes scalar RecipeData fields to RecipeHashLookupMap.
    /// MUST be called AFTER StationSystem.ApplyChanges() completes — its
    /// RegisterRecipes()/RegisterGameData() calls rebuild the map from baked
    /// data and would otherwise wipe these writes. This is the final ECS
    /// mutation in the Cookbook init sequence.
    ///
    /// The crafting completion system reads CraftDuration from this map, so
    /// these writes are what actually make custom durations take effect at
    /// completion time (the entity write only drives the countdown display).
    ///
    /// [PERFORMANCE] One map read + one map write per changed recipe.
    ///               Runs once at startup only.
    /// </summary>
    public static void ApplyMapValues()
    {
        var config = CookbookPlugin.RecipeData;
        if (config == null || config.Recipes.Count == 0) return;

        var map = Heart.GameDataSystem.RecipeHashLookupMap;
        int applied = 0;

        foreach (var (recipeName, entry) in config.Recipes)
        {
            if (!entry.ChangesEnabled) continue;

            bool hasScalar =
                entry.CraftDuration.HasValue       ||
                entry.AlwaysUnlocked.HasValue       ||
                entry.HideInStation.HasValue        ||
                entry.IgnoreServerSettings.HasValue ||
                entry.HudSortingOrder.HasValue;

            if (!hasScalar) continue;

            if (!PrefabNameResolver.TryResolve(recipeName, out PrefabGUID guid))
                continue;

            if (map.TryGetValue(guid, out var mapEntry))
            {
                if (entry.CraftDuration.HasValue)       mapEntry.CraftDuration        = entry.CraftDuration.Value;
                if (entry.AlwaysUnlocked.HasValue)       mapEntry.AlwaysUnlocked       = entry.AlwaysUnlocked.Value;
                if (entry.HideInStation.HasValue)        mapEntry.HideInStation        = entry.HideInStation.Value;
                if (entry.IgnoreServerSettings.HasValue) mapEntry.IgnoreServerSettings = entry.IgnoreServerSettings.Value;
                if (entry.HudSortingOrder.HasValue)      mapEntry.HudSortingOrder      = entry.HudSortingOrder.Value;
                map[guid] = mapEntry;
                applied++;

                HeartLogger.Debug(LOG_SOURCE,
                    $"[Final] RecipeHashLookupMap '{guid._Value}': " +
                    $"CraftDuration={mapEntry.CraftDuration} AlwaysUnlocked={mapEntry.AlwaysUnlocked}");
            }
            else
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"[Final] Recipe GUID {guid._Value} not found in RecipeHashLookupMap — " +
                    "scalar fields may not apply.");
            }
        }

        HeartLogger.Info(LOG_SOURCE,
            $"Final RecipeHashLookupMap pass applied scalar fields to {applied} recipe(s).");
    }

    // ── Soul override builder ─────────────────────────────────

    /// <summary>
    /// Builds a LilithRecipeData for Soul sync. CraftDuration is taken from
    /// the config entry (not the entity) since the map/entity may be in flux
    /// during the multi-system init sequence — the config is the source of truth.
    /// </summary>
    static LilithRecipeData BuildSoulOverride(Entity recipeEntity, RecipeEntryData entry)
    {
        var result = new LilithRecipeData();

        // [CHANGED] Prefer the config value so Soul always receives the intended
        // duration regardless of ECS map/entity state during init.
        if (entry.CraftDuration.HasValue)
            result.CraftDuration = entry.CraftDuration.Value;
        else if (recipeEntity.TryGetComponent<RecipeData>(out var recipeData))
            result.CraftDuration = recipeData.CraftDuration;

        if (recipeEntity.TryGetBuffer<RecipeRequirementBuffer>(out var reqBuffer))
        {
            result.Requirements = new Dictionary<string, int>(reqBuffer.Length);
            for (int i = 0; i < reqBuffer.Length; i++)
            {
                var req = reqBuffer[i];
                PrefabNameResolver.TryResolveName(req.Guid, out string itemName);
                result.Requirements[string.IsNullOrEmpty(itemName)
                    ? req.Guid._Value.ToString() : itemName] = req.Amount;
            }
        }

        if (recipeEntity.TryGetBuffer<RecipeOutputBuffer>(out var outBuffer))
        {
            result.Outputs = new Dictionary<string, int>(outBuffer.Length);
            for (int i = 0; i < outBuffer.Length; i++)
            {
                var output = outBuffer[i];
                PrefabNameResolver.TryResolveName(output.Guid, out string itemName);
                result.Outputs[string.IsNullOrEmpty(itemName)
                    ? output.Guid._Value.ToString() : itemName] = output.Amount;
            }
        }

        return result;
    }

    // ── Per-field apply ───────────────────────────────────────

    /// <summary>
    /// Writes scalar RecipeData fields to the prefab entity component ONLY.
    /// Map writes are handled separately by ApplyMapValues() after all
    /// registration calls (including StationSystem's) have completed.
    /// </summary>
    static void ApplyRecipeEntityOnly(Entity recipeEntity, RecipeEntryData entry)
    {
        var data = recipeEntity.Read<RecipeData>();

        if (entry.CraftDuration.HasValue)       data.CraftDuration        = entry.CraftDuration.Value;
        if (entry.AlwaysUnlocked.HasValue)       data.AlwaysUnlocked       = entry.AlwaysUnlocked.Value;
        if (entry.HideInStation.HasValue)        data.HideInStation        = entry.HideInStation.Value;
        if (entry.IgnoreServerSettings.HasValue) data.IgnoreServerSettings = entry.IgnoreServerSettings.Value;
        if (entry.HudSortingOrder.HasValue)      data.HudSortingOrder      = entry.HudSortingOrder.Value;

        recipeEntity.Write(data);
    }

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

    static void RemoveBuffer<T>(Entity recipeEntity, string recipeName) where T : class
    {
        if (typeof(T) == typeof(List<CookbookItemData>))
        {
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