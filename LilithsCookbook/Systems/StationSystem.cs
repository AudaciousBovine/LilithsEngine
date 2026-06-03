// ============================================================
//  StationSystem — LilithsCookbook
//  LilithsCookbook/Systems/StationSystem.cs
//
//  Applies crafting station recipe changes derived from each
//  RecipeEntryData.Stations list in the Recipes config.
//
//  [CHANGED] No longer reads CookbookStationData / StationEntryData.
//            Station membership is now declared inline on each
//            recipe via its Stations list. StationSystem iterates
//            CookbookPlugin.RecipeData to build a per-station
//            add/remove diff, then applies it.
//
//  How Stations diff works:
//  ─────────────────────────
//  For each recipe entry with a non-null Stations list:
//    • null   → skip (no station changes for this recipe)
//    • []     → remove this recipe from every station that has it
//    • [...]  → add to each listed station; remove from any station
//               NOT listed that currently carries the recipe in ECS
//
//  The diff is built by scanning the prefab entity of every known
//  station (WorkstationRecipesBuffer + RefinementstationRecipesBuffer)
//  for the recipe GUID, then building per-station add/remove lists.
//  This produces an equivalent result to the old separate Stations
//  config but authored at the recipe level.
//
//  Two-pass approach (unchanged):
//  ───────────────────────────────
//  Pass 1: Patch all prefab entities.
//  Registration: RegisterRecipes() + RegisterGameData().
//  Pass 2: Patch live User entities + batched placed station scan.
//
//  Why two passes and why GetAllEntities():
//  ──────────────────────────────────────────
//  RegisterGameData() resets WorkstationRecipesBuffer on live
//  entities but not prefab entities. V Rising keeps the
//  Unity.Entities.Prefab tag on placed world instances, making
//  None=[Prefab] query exclusion ineffective. GetAllEntities()
//  with direct prefab-entity identity exclusion is required.
//
//  [PERFORMANCE] All ECS operations run once at startup only.
//                The diff-build pass is O(recipes × stations) at
//                startup — negligible for config-scale input.
//                Single GetAllEntities() scan covers all stations.
// ============================================================

using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using LilithsHeart.Foundation;
using LilithsHeart.Services;
using LilithsCookbook.Data;

namespace LilithsCookbook.Systems;

public static class StationSystem
{
    private const string LOG_SOURCE = "LilithsCookbook.StationSystem";

    // ── Public entry point ────────────────────────────────────────────────────

    public static void ApplyChanges()
    {
        var recipeData = CookbookPlugin.RecipeData;

        if (recipeData == null || recipeData.Recipes.Count == 0)
        {
            HeartLogger.Info(LOG_SOURCE, "No recipe data — skipping station patching.");
            return;
        }

        // Build the per-station diff from inline Stations lists.
        // [CHANGED] Replaces reading CookbookStationData. We now derive
        //           station membership from each recipe's own Stations list.
        var stationDiff = BuildStationDiff(recipeData);

        if (stationDiff.Count == 0)
        {
            HeartLogger.Info(LOG_SOURCE, "No station membership changes in recipe config.");
            return;
        }

        // ── Pass 1: Patch all prefab entities ─────────────────────────────────
        // Patches RefinementstationRecipesBuffer and WorkstationRecipesBuffer
        // prefab entities. RegisterGameData() resets WorkstationRecipesBuffer on
        // live entities after this — prefab patches survive unaffected.

        foreach (var (stationName, diff) in stationDiff)
        {
            if (!PrefabNameResolver.TryResolve(stationName, out PrefabGUID guid))
            {
                HeartLogger.Warning(LOG_SOURCE, $"Could not resolve station: '{stationName}'");
                continue;
            }

            if (!Heart.PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(guid, out Entity stationEntity))
            {
                HeartLogger.Warning(LOG_SOURCE, $"Could not find prefab entity for station: '{stationName}'");
                continue;
            }

            bool hasRefinement  = stationEntity.Has<RefinementstationRecipesBuffer>();
            bool hasWorkstation = stationEntity.Has<WorkstationRecipesBuffer>();

            if (!hasRefinement && !hasWorkstation)
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"'{stationName}' has neither RefinementstationRecipesBuffer nor " +
                    $"WorkstationRecipesBuffer — skipping.");
                continue;
            }

            if (diff.ToAdd.Count > 0)
            {
                if (hasRefinement)
                    AddRefinementRecipes(stationEntity, diff.ToAdd, stationName);
                else
                    AddWorkstationRecipes(stationEntity, diff.ToAdd, stationName);
            }

            if (diff.ToRemove.Count > 0)
            {
                if (hasRefinement)
                    RemoveRefinementRecipes(stationEntity, diff.ToRemove, stationName);
                else
                    RemoveWorkstationRecipes(stationEntity, diff.ToRemove, stationName);
            }

            HeartLogger.Info(LOG_SOURCE,
                $"[Pass 1] Patched prefab '{stationName}': " +
                $"+{diff.ToAdd.Count} / -{diff.ToRemove.Count} recipe(s).");
        }

        // ── Registration ──────────────────────────────────────────────────────
        Heart.GameDataSystem.RegisterRecipes();
        Heart.PrefabCollectionSystem.RegisterGameData();

        // ── Pass 2: Patch all live entities ───────────────────────────────────
        // Build the workstation live-patch targets map, handle user entities,
        // then run the single batched GetAllEntities() scan.

        var workstationTargets =
            new Dictionary<int, (string Name, StationDiff Diff, Entity PrefabEntity)>();

        int changed = 0;

        foreach (var (stationName, diff) in stationDiff)
        {
            if (!PrefabNameResolver.TryResolve(stationName, out PrefabGUID guid)) continue;
            if (!Heart.PrefabCollectionSystem._PrefabGuidToEntityMap.TryGetValue(
                    guid, out Entity stationEntity)) continue;

            bool hasWorkstation = stationEntity.Has<WorkstationRecipesBuffer>();
            bool isPlayerEntity = stationEntity.Has<ProjectM.Network.User>();

            if (!hasWorkstation) continue;

            if (isPlayerEntity)
            {
                // User entity patching — cheap targeted query, no batching needed.
                PatchLiveUserEntities(diff.ToAdd, diff.ToRemove, stationName);
                Heart.RegisterPlayerRecipeChanges(diff.ToAdd, diff.ToRemove);
                HeartLogger.Info(LOG_SOURCE,
                    $"[{stationName}] Registered +{diff.ToAdd.Count}/-{diff.ToRemove.Count} " +
                    "player recipe change(s) with Heart for Soul sync.");
                changed++;
            }
            else
            {
                // Queue for the batched live station scan.
                workstationTargets[guid._Value] = (stationName, diff, stationEntity);
                Heart.RegisterStationRecipeChanges(stationName, diff.ToAdd, diff.ToRemove);
                changed++;
            }
        }

        // Single batched scan for all WorkstationRecipesBuffer placed stations.
        // [PERFORMANCE] One GetAllEntities() covers all stations — O(entities),
        //               not O(entities × station count).
        if (workstationTargets.Count > 0)
            PatchAllLiveStationEntities(workstationTargets);

        HeartLogger.Info(LOG_SOURCE, $"Station patching complete — {changed} station(s) modified.");
    }

    // ── Diff builder ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a per-station add/remove diff by iterating all recipes that
    /// have a non-null Stations list.
    ///
    /// For each such recipe:
    ///   • The recipe is added to every station in its Stations list.
    ///   • The recipe is removed from every station that currently holds it
    ///     in ECS but is NOT in its Stations list (unless Stations is null,
    ///     which means "don't touch").
    ///   • Stations: [] means remove from every station that has it.
    ///
    /// [PERFORMANCE] O(recipes) outer loop, O(stations) inner scan per recipe.
    ///               All lookups are dictionary reads — O(1) each.
    ///               Runs once at startup.
    /// </summary>
    static Dictionary<string, StationDiff> BuildStationDiff(CookbookRecipeData recipeData)
    {
        var diff = new Dictionary<string, StationDiff>(StringComparer.Ordinal);

        // Build a lookup of every known station prefab entity and its current
        // recipe buffers — used to find stations that currently hold a recipe
        // so we can generate removes for stations not in the declared list.
        var allStations = BuildStationInventory();

        foreach (var (recipeName, entry) in recipeData.Recipes)
        {
            // Null means "don't touch station membership for this recipe".
            if (entry.Stations == null) continue;
            if (!entry.ChangesEnabled)
            {
                HeartLogger.Debug(LOG_SOURCE,
                    $"Skipping station diff for '{recipeName}' — ChangesEnabled = false.");
                continue;
            }

            if (!PrefabNameResolver.TryResolve(recipeName, out PrefabGUID recipeGuid))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"Could not resolve recipe '{recipeName}' for station diff — skipping.");
                continue;
            }

            // Collect declared target stations as a set for O(1) membership check.
            var declaredSet = new HashSet<string>(entry.Stations, StringComparer.Ordinal);

            // Add to every declared station.
            foreach (var stationName in entry.Stations)
            {
                var d = GetOrCreate(diff, stationName);
                if (!d.ToAdd.Contains(recipeName))
                    d.ToAdd.Add(recipeName);
            }

            // Remove from any station that currently has the recipe but is
            // not in the declared list. Also handles Stations: [] (declaredSet empty).
            foreach (var (stationName, currentRecipes) in allStations)
            {
                if (declaredSet.Contains(stationName)) continue;

                if (currentRecipes.Contains(recipeGuid._Value))
                {
                    var d = GetOrCreate(diff, stationName);
                    if (!d.ToRemove.Contains(recipeName))
                        d.ToRemove.Add(recipeName);
                }
            }
        }

        return diff;
    }

    /// <summary>
    /// Returns a map of station prefab name → set of recipe GUID int values
    /// currently in that station's recipe buffer (Workstation or Refinement).
    /// Used by BuildStationDiff to find existing recipe membership.
    ///
    /// [PERFORMANCE] O(stations × recipes per station) — runs once at startup.
    ///               Iterates PrefabGuidToEntityMap, which is pre-built by the game.
    /// </summary>
    static Dictionary<string, HashSet<int>> BuildStationInventory()
    {
        var inventory = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        var prefabMap = Heart.PrefabCollectionSystem._PrefabGuidToEntityMap;

        foreach (var kvp in prefabMap)
        {
            var entity = kvp.Value;

            // Try to get a human-readable name for this station.
            if (!PrefabNameResolver.TryResolveName(kvp.Key, out string stationName))
                continue;

            HashSet<int>? recipes = null;

            if (entity.Has<WorkstationRecipesBuffer>())
            {
                var buffer = entity.ReadBuffer<WorkstationRecipesBuffer>();
                recipes = new HashSet<int>(buffer.Length);
                for (int i = 0; i < buffer.Length; i++)
                    recipes.Add(buffer[i].RecipeGuid._Value);
            }
            else if (entity.Has<RefinementstationRecipesBuffer>())
            {
                var buffer = entity.ReadBuffer<RefinementstationRecipesBuffer>();
                recipes = new HashSet<int>(buffer.Length);
                for (int i = 0; i < buffer.Length; i++)
                    recipes.Add(buffer[i].RecipeGuid._Value);
            }

            if (recipes != null)
                inventory[stationName] = recipes;
        }

        return inventory;
    }

    static StationDiff GetOrCreate(Dictionary<string, StationDiff> diff, string stationName)
    {
        if (!diff.TryGetValue(stationName, out var d))
        {
            d = new StationDiff();
            diff[stationName] = d;
        }
        return d;
    }

    // ── Live User entity patching ─────────────────────────────────────────────

    static void PatchLiveUserEntities(
        List<string> addRecipes,
        List<string> removeRecipes,
        string stationName)
    {
        var em = Heart.EntityManager;

        var query = em.CreateEntityQuery(
            ComponentType.ReadWrite<WorkstationRecipesBuffer>(),
            ComponentType.ReadOnly<ProjectM.Network.User>()
        );

        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

        try
        {
            HeartLogger.Info(LOG_SOURCE,
                $"[{stationName}] Patching {entities.Length} live User entity(s).");

            foreach (var userEntity in entities)
            {
                if (addRecipes.Count > 0)
                    AddWorkstationRecipes(userEntity, addRecipes, stationName);

                if (removeRecipes.Count > 0)
                    RemoveWorkstationRecipes(userEntity, removeRecipes, stationName);
            }
        }
        finally
        {
            entities.Dispose();
        }
    }

    // ── Batched live placed station entity patching ───────────────────────────

    /// <summary>
    /// Patches WorkstationRecipesBuffer on all placed world instances of all
    /// stations in a single GetAllEntities() scan.
    ///
    /// V Rising keeps Unity.Entities.Prefab on placed world instances
    /// so None=[Prefab] query exclusion is ineffective. GetAllEntities()
    /// with direct prefab entity identity exclusion is required.
    ///
    /// [PERFORMANCE] One GetAllEntities() scan at startup covering all stations.
    ///               O(entities) regardless of how many stations are configured.
    /// </summary>
    static void PatchAllLiveStationEntities(
        Dictionary<int, (string Name, StationDiff Diff, Entity PrefabEntity)> targets)
    {
        var em          = Heart.EntityManager;
        var allEntities = em.GetAllEntities(Unity.Collections.Allocator.Temp);

        var patchedCounts = new Dictionary<int, int>();
        foreach (var guid in targets.Keys)
            patchedCounts[guid] = 0;

        try
        {
            foreach (var entity in allEntities)
            {
                if (!em.HasComponent<PrefabGUID>(entity)) continue;

                var entityGuid = em.GetComponentData<PrefabGUID>(entity);

                if (!targets.TryGetValue(entityGuid._Value, out var target)) continue;
                if (!em.HasBuffer<WorkstationRecipesBuffer>(entity)) continue;

                // Skip the prefab template — already patched in Pass 1.
                if (entity == target.PrefabEntity) continue;

                if (target.Diff.ToAdd.Count > 0)
                    AddWorkstationRecipes(entity, target.Diff.ToAdd, target.Name);

                if (target.Diff.ToRemove.Count > 0)
                    RemoveWorkstationRecipes(entity, target.Diff.ToRemove, target.Name);

                patchedCounts[entityGuid._Value]++;
            }
        }
        finally
        {
            allEntities.Dispose();
        }

        foreach (var (guidValue, (name, diff, _)) in targets)
        {
            HeartLogger.Info(LOG_SOURCE,
                $"[Pass 2] '{name}': patched {patchedCounts[guidValue]} live instance(s) " +
                $"(+{diff.ToAdd.Count}/-{diff.ToRemove.Count}).");
        }
    }

    // ── RefinementstationRecipesBuffer helpers ────────────────────────────────

    static void AddRefinementRecipes(Entity stationEntity, List<string> recipes, string stationName)
    {
        var buffer = stationEntity.ReadBuffer<RefinementstationRecipesBuffer>();

        foreach (var recipeName in recipes)
        {
            if (!PrefabNameResolver.TryResolve(recipeName, out PrefabGUID recipeGuid))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"[{stationName}] Could not resolve recipe to add: '{recipeName}' — skipping.");
                continue;
            }

            bool alreadyExists = false;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].RecipeGuid.Equals(recipeGuid)) { alreadyExists = true; break; }
            }

            if (!alreadyExists)
            {
                buffer.Add(new RefinementstationRecipesBuffer { RecipeGuid = recipeGuid });
                HeartLogger.Info(LOG_SOURCE, $"[{stationName}] Added refinement recipe '{recipeName}'.");
            }
        }
    }

    static void RemoveRefinementRecipes(Entity stationEntity, List<string> recipes, string stationName)
    {
        var buffer = stationEntity.ReadBuffer<RefinementstationRecipesBuffer>();

        foreach (var recipeName in recipes)
        {
            if (!PrefabNameResolver.TryResolve(recipeName, out PrefabGUID recipeGuid))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"[{stationName}] Could not resolve recipe to remove: '{recipeName}' — skipping.");
                continue;
            }

            bool found = false;
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].RecipeGuid.Equals(recipeGuid))
                {
                    buffer.RemoveAt(i);
                    HeartLogger.Info(LOG_SOURCE, $"[{stationName}] Removed refinement recipe '{recipeName}'.");
                    found = true;
                    break;
                }
            }

            if (!found)
                HeartLogger.Debug(LOG_SOURCE,
                    $"[{stationName}] Refinement recipe '{recipeName}' not found — nothing to remove.");
        }
    }

    // ── WorkstationRecipesBuffer helpers ──────────────────────────────────────

    static void AddWorkstationRecipes(Entity stationEntity, List<string> recipes, string stationName)
    {
        var buffer = stationEntity.ReadBuffer<WorkstationRecipesBuffer>();

        foreach (var recipeName in recipes)
        {
            if (!PrefabNameResolver.TryResolve(recipeName, out PrefabGUID recipeGuid))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"[{stationName}] Could not resolve recipe to add: '{recipeName}' — skipping.");
                continue;
            }

            bool alreadyExists = false;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].RecipeGuid.Equals(recipeGuid)) { alreadyExists = true; break; }
            }

            if (!alreadyExists)
            {
                buffer.Add(new WorkstationRecipesBuffer { RecipeGuid = recipeGuid });
                HeartLogger.Info(LOG_SOURCE, $"[{stationName}] Added workstation recipe '{recipeName}'.");
            }
        }
    }

    static void RemoveWorkstationRecipes(Entity stationEntity, List<string> recipes, string stationName)
    {
        var buffer = stationEntity.ReadBuffer<WorkstationRecipesBuffer>();

        foreach (var recipeName in recipes)
        {
            if (!PrefabNameResolver.TryResolve(recipeName, out PrefabGUID recipeGuid))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"[{stationName}] Could not resolve recipe to remove: '{recipeName}' — skipping.");
                continue;
            }

            bool found = false;
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].RecipeGuid.Equals(recipeGuid))
                {
                    buffer.RemoveAt(i);
                    HeartLogger.Info(LOG_SOURCE, $"[{stationName}] Removed workstation recipe '{recipeName}'.");
                    found = true;
                    break;
                }
            }

            if (!found)
                HeartLogger.Debug(LOG_SOURCE,
                    $"[{stationName}] Workstation recipe '{recipeName}' not found — nothing to remove.");
        }
    }

    // ── Internal diff record ─────────────────────────────────────────────────

    /// <summary>
    /// Per-station add/remove lists built by BuildStationDiff().
    /// Both lists hold recipe prefab name strings (not GUIDs) —
    /// resolved by the helper methods at apply time so error
    /// messages are readable.
    ///
    /// Private nested class — used only within StationSystem.
    /// Declared here rather than file-scoped to satisfy CS9051:
    /// file-local types cannot appear in member signatures of
    /// non-file-local types.
    /// </summary>
    sealed class StationDiff
    {
        public List<string> ToAdd    { get; } = new();
        public List<string> ToRemove { get; } = new();
    }
}