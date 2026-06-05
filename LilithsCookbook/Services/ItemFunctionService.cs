// ============================================================
//  ItemFunctionService — LilithsCookbook
//  LilithsCookbook/Services/ItemFunctionService.cs
//
//  Applies item functional overrides to server-side ECS prefab
//  entities AND GameDataSystem.ItemHashLookupMap. Reads StackSize
//  from LilithItemConfig.Overrides (populated by Heart's ItemService)
//  and patches ProjectM.ItemData.MaxAmount.
//
//  Ownership:
//  ───────────
//  StackSize is a Cookbook concern — it belongs alongside recipe
//  and station configuration as a server-side item property.
//  Heart's ItemService loads the value from JSON into
//  LilithItemConfig but does not act on it. This service is the
//  sole actor for StackSize.
//
//  Server-side only:
//  ──────────────────
//  StackSize is never synced to Soul. ItemData.MaxAmount is
//  enforced server-side by the ECS inventory system. The client
//  reads stack limits from the server's ECS state.
//
//  Why BOTH the entity AND the map:
//  ─────────────────────────────────
//  [CHANGED] The inventory system reads MaxAmount from
//  GameDataSystem.ItemHashLookupMap (a NativeParallelHashMap<PrefabGUID,
//  ItemData>), NOT from the prefab entity component. This is the exact
//  same entity-vs-map split as RecipeHashLookupMap for crafting:
//    • Entity component write → drives some display paths
//    • Map write             → what the inventory system actually enforces
//  Writing only the prefab entity (the previous behaviour) left the map
//  holding the vanilla MaxAmount, so stacks were never actually limited.
//  See CONVENTIONS.md "ECS Write Ordering" — the map write must be the
//  final mutation, after every RegisterGameData() call. ItemFunctionService
//  runs last in CookbookPlugin.OnHeartInitialized() (after StationSystem's
//  RegisterGameData()), so the map write here is safe.
//
//  ECS approach:
//  ─────────────
//  Item prefab entities live in PrefabCollectionSystem._PrefabGuidToEntityMap.
//  A single write at startup is sufficient for both the entity and the map —
//  item prefabs have no live world instances that get reset, and this service
//  runs after all RegisterGameData() calls.
//
//  [PERFORMANCE] Runs once at startup. O(configured items).
//                One entity write + one map write per item. No per-frame cost.
// ============================================================

using ProjectM;
using Stunlock.Core;
using LilithsHeart.Config;
using LilithsHeart.Foundation;
using LilithsHeart.Services;

namespace LilithsCookbook.Services;

public static class ItemFunctionService
{
    private const string LOG_SOURCE = "LilithsCookbook.ItemFunctionService";

    /// <summary>
    /// Reads StackSize from LilithItemConfig and patches ItemData.MaxAmount
    /// on each item's prefab entity AND in GameDataSystem.ItemHashLookupMap.
    /// Called LAST from CookbookPlugin.OnHeartInitialized() — after all
    /// RegisterGameData() calls — so the map write is not reset.
    /// </summary>
    public static void ApplyOverrides()
    {
        var overrides = LilithItemConfig.Overrides;

        // Filter to only entries that have a StackSize configured.
        // [PERFORMANCE] Where() + ToList() once at startup — negligible.
        var stackEntries = overrides
            .Where(kvp => kvp.Value.StackSize.HasValue)
            .ToList();

        if (stackEntries.Count == 0)
        {
            HeartLogger.Info(LOG_SOURCE, "No StackSize overrides configured.");
            return;
        }

        var prefabMap = Heart.PrefabCollectionSystem._PrefabGuidToEntityMap;

        // [CHANGED] The authoritative stack-size source read by the inventory
        // system. Patched in addition to the prefab entity component.
        var itemMap = Heart.GameDataSystem.ItemHashLookupMap;

        int patched = 0;
        int failed  = 0;

        foreach (var (itemName, data) in stackEntries)
        {
            if (!PrefabNameResolver.TryResolve(itemName, out PrefabGUID guid))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"Could not resolve item '{itemName}' — not in LilithsMind definitions. Skipping.");
                failed++;
                continue;
            }

            int newMax = data.StackSize!.Value;
            bool any   = false;

            // ── Prefab entity component write ─────────────────────────────
            if (prefabMap.TryGetValue(guid, out var entity) && entity.Has<ItemData>())
            {
                var itemData = entity.Read<ItemData>();
                itemData.MaxAmount = newMax;
                entity.Write(itemData);
                any = true;
            }
            else
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"'{itemName}' has no prefab entity with ItemData — entity write skipped.");
            }

            // ── ItemHashLookupMap write (authoritative for inventory) ─────
            // [CHANGED] This is the write that actually enforces the stack limit.
            if (itemMap.TryGetValue(guid, out var mapItemData))
            {
                mapItemData.MaxAmount = newMax;
                itemMap[guid] = mapItemData;
                any = true;
            }
            else
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"'{itemName}' (GUID {guid._Value}) not found in ItemHashLookupMap — " +
                    "stack limit may not be enforced.");
            }

            if (any)
            {
                HeartLogger.Info(LOG_SOURCE,
                    $"[StackSize] '{itemName}' MaxAmount → {newMax} (entity + map).");
                patched++;
            }
            else
            {
                failed++;
            }
        }

        HeartLogger.Info(LOG_SOURCE,
            $"StackSize patching complete — {patched} patched, {failed} failed.");
    }
}