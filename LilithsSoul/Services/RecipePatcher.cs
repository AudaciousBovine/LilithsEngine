// ============================================================
//  RecipePatcher — LilithsSoul
//  LilithsSoul/Services/RecipePatcher.cs
//
//  Patches client-side prefab ECS entities to reflect the recipe
//  overrides received from LilithsHeart via ServerSyncPayload.
//
//  Why this is needed:
//  ───────────────────
//  V Rising's WorkstationSubMenu reads RecipeRequirementBuffer,
//  RecipeOutputBuffer, and RecipeData.CraftDuration directly from
//  client-local prefab entities loaded at startup. There is no
//  network sync mechanism for these buffers — the server enforces
//  the correct values but the client HUD always shows vanilla.
//  RecipePatcher fixes this by patching the client's local prefab
//  entities after receiving Heart's overrides on connect.
//
//  [CHANGED] PatchRequirementBuffer and PatchOutputBuffer now
//            iterate Dictionary<string, int> (prefab name → amount)
//            matching the simplified LilithRecipeData structure.
//
//  [PERFORMANCE] BuildNameMap() runs once at world ready — O(n).
//                Apply() iterates only overridden recipes.
//                No per-frame cost after initialization.
// ============================================================

using System.Reflection;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using LilithsSoul.Foundation;
using LilithsMind.Data;

namespace LilithsSoul.Services;

public static class RecipePatcher
{
    private const string LOG_SOURCE = "LilithsSoul.RecipePatcher";

    static readonly Dictionary<string, PrefabGUID> _nameToGuid = new();

    // ── Public API ───────────────────────────────────────────

    public static void BuildNameMap()
    {
        _nameToGuid.Clear();

        var world = Soul.ClientWorld;
        if (world == null)
        {
            SoulLogger.Warning(LOG_SOURCE, "Client world not available — cannot build name→GUID map.");
            return;
        }

        var prefabSystem = world.GetExistingSystemManaged<PrefabCollectionSystem>();
        if (prefabSystem == null)
        {
            SoulLogger.Warning(LOG_SOURCE, "PrefabCollectionSystem not found — cannot build name→GUID map.");
            return;
        }

        // ── Pass 1: AssetName → GUID from ECS ────────────────
        var lookup = prefabSystem._PrefabDataLookup;
        var keys   = lookup.GetKeyArray(Allocator.Temp);
        var vals   = lookup.GetValueArray(Allocator.Temp);

        try
        {
            for (int i = 0; i < keys.Length; i++)
            {
                var assetName = vals[i].AssetName.Value;
                if (!string.IsNullOrEmpty(assetName))
                    _nameToGuid[assetName] = keys[i];
            }
        }
        finally
        {
            keys.Dispose();
            vals.Dispose();
        }

        // ── Pass 2: LilithsMind Name alias → GUID ────────────
        var mindAssembly = typeof(LilithsMind.Prefabs.PrefabDef).Assembly;

        var definitionTypes = mindAssembly
            .GetTypes()
            .Where(t =>
                t.IsClass &&
                t.IsAbstract &&
                t.IsSealed &&
                t.Namespace == "LilithsMind.Prefabs.Definitions")
            .ToList();

        foreach (var type in definitionTypes)
        {
            var fields = type
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(LilithsMind.Prefabs.PrefabDef));

            foreach (var field in fields)
            {
                var def = (LilithsMind.Prefabs.PrefabDef)field.GetValue(null)!;
                if (!string.IsNullOrEmpty(def.Name))
                {
                    var guid = new PrefabGUID(def.GuidHash);
                    _nameToGuid[def.Name] = guid;
                }
            }
        }

        SoulLogger.Info(LOG_SOURCE,
            $"Name→GUID map built with {_nameToGuid.Count} entries " +
            $"(AssetName + LilithsMind aliases).");
    }

    public static void ApplyPlayerRecipes(List<string> toAdd, List<string> toRemove)
    {
        if (toAdd.Count == 0 && toRemove.Count == 0)
        {
            SoulLogger.Debug(LOG_SOURCE, "No player recipe changes to apply.");
            return;
        }

        var world = Soul.ClientWorld;
        if (world == null)
        {
            SoulLogger.Error(LOG_SOURCE, "Client world not ready — cannot patch player recipes.");
            return;
        }

        var em = world.EntityManager;

        var query = em.CreateEntityQuery(
            ComponentType.ReadWrite<WorkstationRecipesBuffer>(),
            ComponentType.ReadOnly<ProjectM.Network.User>()
        );

        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

        try
        {
            if (entities.Length == 0)
            {
                SoulLogger.Warning(LOG_SOURCE,
                    "No player User entity with WorkstationRecipesBuffer found on client.");
                return;
            }

            int patchedCount = 0;

            foreach (var userEntity in entities)
            {
                if (!em.HasBuffer<WorkstationRecipesBuffer>(userEntity)) continue;

                var buffer = em.GetBuffer<WorkstationRecipesBuffer>(userEntity);

                foreach (var recipeName in toAdd)
                {
                    if (!_nameToGuid.TryGetValue(recipeName, out PrefabGUID recipeGuid))
                    {
                        SoulLogger.Warning(LOG_SOURCE,
                            $"[PlayerRecipes] Could not resolve recipe to add: '{recipeName}' — skipping.");
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
                        SoulLogger.Debug(LOG_SOURCE, $"[PlayerRecipes] Added '{recipeName}'.");
                    }
                }

                foreach (var recipeName in toRemove)
                {
                    if (!_nameToGuid.TryGetValue(recipeName, out PrefabGUID recipeGuid))
                    {
                        SoulLogger.Warning(LOG_SOURCE,
                            $"[PlayerRecipes] Could not resolve recipe to remove: '{recipeName}' — skipping.");
                        continue;
                    }

                    for (int i = buffer.Length - 1; i >= 0; i--)
                    {
                        if (buffer[i].RecipeGuid.Equals(recipeGuid))
                        {
                            buffer.RemoveAt(i);
                            SoulLogger.Debug(LOG_SOURCE, $"[PlayerRecipes] Removed '{recipeName}'.");
                            break;
                        }
                    }
                }

                patchedCount++;
            }

            SoulLogger.Info(LOG_SOURCE,
                $"Player recipe patching complete — {patchedCount} entity(s) patched, " +
                $"{toAdd.Count} add(s), {toRemove.Count} remove(s).");
        }
        finally
        {
            entities.Dispose();
        }
    }

    public static void ApplyStationRecipes(Dictionary<string, LilithStationData> overrides)
    {
        if (overrides.Count == 0)
        {
            SoulLogger.Debug(LOG_SOURCE, "No station recipe overrides to apply.");
            return;
        }

        var world = Soul.ClientWorld;
        if (world == null)
        {
            SoulLogger.Error(LOG_SOURCE, "Client world not ready — cannot patch station recipes.");
            return;
        }

        var prefabSystem = world.GetExistingSystemManaged<PrefabCollectionSystem>();
        if (prefabSystem == null)
        {
            SoulLogger.Error(LOG_SOURCE, "PrefabCollectionSystem not found — cannot patch station recipes.");
            return;
        }

        var em        = world.EntityManager;
        var prefabMap = prefabSystem._PrefabGuidToEntityMap;
        int patched   = 0;
        int failed    = 0;

        foreach (var (stationName, data) in overrides)
        {
            if (!_nameToGuid.TryGetValue(stationName, out PrefabGUID stationGuid))
            {
                SoulLogger.Warning(LOG_SOURCE,
                    $"[StationRecipes] Could not resolve station '{stationName}' — skipping.");
                failed++;
                continue;
            }

            if (!prefabMap.TryGetValue(stationGuid, out Entity stationEntity))
            {
                SoulLogger.Warning(LOG_SOURCE,
                    $"[StationRecipes] Could not find prefab entity for '{stationName}' — skipping.");
                failed++;
                continue;
            }

            if (!em.HasBuffer<WorkstationRecipesBuffer>(stationEntity))
            {
                SoulLogger.Warning(LOG_SOURCE,
                    $"[StationRecipes] '{stationName}' prefab has no WorkstationRecipesBuffer — skipping.");
                failed++;
                continue;
            }

            var buffer = em.GetBuffer<WorkstationRecipesBuffer>(stationEntity);

            foreach (var recipeName in data.RecipesToAdd)
            {
                if (!_nameToGuid.TryGetValue(recipeName, out PrefabGUID recipeGuid))
                {
                    SoulLogger.Warning(LOG_SOURCE,
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
                    SoulLogger.Debug(LOG_SOURCE, $"[{stationName}] Added '{recipeName}'.");
                }
            }

            foreach (var recipeName in data.RecipesToRemove)
            {
                if (!_nameToGuid.TryGetValue(recipeName, out PrefabGUID recipeGuid))
                {
                    SoulLogger.Warning(LOG_SOURCE,
                        $"[{stationName}] Could not resolve recipe to remove: '{recipeName}' — skipping.");
                    continue;
                }

                for (int i = buffer.Length - 1; i >= 0; i--)
                {
                    if (buffer[i].RecipeGuid.Equals(recipeGuid))
                    {
                        buffer.RemoveAt(i);
                        SoulLogger.Debug(LOG_SOURCE, $"[{stationName}] Removed '{recipeName}'.");
                        break;
                    }
                }
            }

            patched++;
        }

        SoulLogger.Info(LOG_SOURCE,
            $"Station recipe patching complete — {patched} station(s) patched, {failed} failed.");
    }

    public static void Apply(Dictionary<string, LilithRecipeData> overrides)
    {
        if (overrides.Count == 0)
        {
            SoulLogger.Debug(LOG_SOURCE, "No recipe overrides to apply.");
            return;
        }

        var world = Soul.ClientWorld;
        if (world == null)
        {
            SoulLogger.Error(LOG_SOURCE, "Client world not ready — cannot patch recipes.");
            return;
        }

        var prefabSystem = world.GetExistingSystemManaged<PrefabCollectionSystem>();
        if (prefabSystem == null)
        {
            SoulLogger.Error(LOG_SOURCE, "PrefabCollectionSystem not found — cannot patch recipes.");
            return;
        }

        var gameDataSystem = world.GetExistingSystemManaged<GameDataSystem>();
        if (gameDataSystem == null)
            SoulLogger.Warning(LOG_SOURCE,
                "Client GameDataSystem not found — CraftDuration display may not update.");

        var prefabMap = prefabSystem._PrefabGuidToEntityMap;
        var em        = world.EntityManager;
        int patched   = 0;
        int failed    = 0;

        foreach (var (recipeName, data) in overrides)
        {
            if (!_nameToGuid.TryGetValue(recipeName, out PrefabGUID recipeGuid))
            {
                SoulLogger.Warning(LOG_SOURCE,
                    $"Could not resolve recipe name '{recipeName}' to GUID — skipping.");
                failed++;
                continue;
            }

            if (!prefabMap.TryGetValue(recipeGuid, out Entity recipeEntity))
            {
                SoulLogger.Warning(LOG_SOURCE,
                    $"Could not find client entity for recipe '{recipeName}' — skipping.");
                failed++;
                continue;
            }

            try
            {
                PatchRecipe(em, recipeEntity, recipeGuid, recipeName, data, gameDataSystem);
                patched++;
            }
            catch (Exception ex)
            {
                SoulLogger.Error(LOG_SOURCE,
                    $"Failed to patch recipe '{recipeName}': {ex.Message}");
                failed++;
            }
        }

        SoulLogger.Info(LOG_SOURCE,
            $"Recipe patching complete — {patched} patched, {failed} failed.");
    }

    // ── Internal ─────────────────────────────────────────────

    static void PatchRecipe(
        EntityManager em,
        Entity recipeEntity,
        PrefabGUID recipeGuid,
        string recipeName,
        LilithRecipeData data,
        GameDataSystem? gameDataSystem)
    {
        if (em.HasComponent<RecipeData>(recipeEntity))
        {
            var recipeData = em.GetComponentData<RecipeData>(recipeEntity);
            recipeData.CraftDuration = data.CraftDuration;
            em.SetComponentData(recipeEntity, recipeData);
        }

        if (gameDataSystem != null)
        {
            var map = gameDataSystem.RecipeHashLookupMap;
            if (map.TryGetValue(recipeGuid, out var mapEntry))
            {
                mapEntry.CraftDuration = data.CraftDuration;
                map[recipeGuid] = mapEntry;
            }
        }

        // RecipeRequirementBuffer — V Rising ECS type, not renamed.
        PatchRequirementBuffer(em, recipeEntity, recipeName, data.Requirements);

        // RecipeOutputBuffer — V Rising ECS type, not renamed.
        PatchOutputBuffer(em, recipeEntity, recipeName, data.Outputs);
    }

    // [CHANGED] Iterates Dictionary<string, int> (prefab name → amount).
    //           RecipeRequirementBuffer is a V Rising ECS type — not renamed.
    static void PatchRequirementBuffer(
        EntityManager em,
        Entity recipeEntity,
        string recipeName,
        Dictionary<string, int> slots)
    {
        DynamicBuffer<RecipeRequirementBuffer> buffer;

        if (em.HasBuffer<RecipeRequirementBuffer>(recipeEntity))
            buffer = em.GetBuffer<RecipeRequirementBuffer>(recipeEntity);
        else
            buffer = em.AddBuffer<RecipeRequirementBuffer>(recipeEntity);

        buffer.Clear();

        foreach (var (itemName, amount) in slots)
        {
            if (!_nameToGuid.TryGetValue(itemName, out PrefabGUID itemGuid))
            {
                SoulLogger.Warning(LOG_SOURCE,
                    $"[{recipeName}] Could not resolve requirement item '{itemName}' — skipping slot.");
                continue;
            }

            buffer.Add(new RecipeRequirementBuffer { Guid = itemGuid, Amount = amount });
        }
    }

    // [CHANGED] Iterates Dictionary<string, int> (prefab name → amount).
    //           RecipeOutputBuffer is a V Rising ECS type — not renamed.
    static void PatchOutputBuffer(
        EntityManager em,
        Entity recipeEntity,
        string recipeName,
        Dictionary<string, int> slots)
    {
        DynamicBuffer<RecipeOutputBuffer> buffer;

        if (em.HasBuffer<RecipeOutputBuffer>(recipeEntity))
            buffer = em.GetBuffer<RecipeOutputBuffer>(recipeEntity);
        else
            buffer = em.AddBuffer<RecipeOutputBuffer>(recipeEntity);

        buffer.Clear();

        foreach (var (itemName, amount) in slots)
        {
            if (!_nameToGuid.TryGetValue(itemName, out PrefabGUID itemGuid))
            {
                SoulLogger.Warning(LOG_SOURCE,
                    $"[{recipeName}] Could not resolve output item '{itemName}' — skipping slot.");
                continue;
            }

            buffer.Add(new RecipeOutputBuffer { Guid = itemGuid, Amount = amount });
        }
    }
}