// ============================================================
//  ItemFunctionService — LilithsCookbook
//  LilithsCookbook/Services/ItemFunctionService.cs
//
//  Applies item functional overrides to server-side ECS prefab
//  entities. Reads StackSize from LilithItemConfig.Overrides
//  (populated by Heart's ItemService) and patches
//  ProjectM.ItemData.MaxAmount on each item's prefab entity.
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
//  ECS approach:
//  ─────────────
//  Item prefab entities live in PrefabCollectionSystem.
//  _PrefabGuidToEntityMap. A single write at startup is sufficient
//  — item prefabs have no live world instances that get reset
//  by RegisterGameData(), unlike workstation recipe buffers.
//  No two-pass required.
//
//  [PERFORMANCE] Runs once at startup. O(configured items).
//                No per-frame cost.
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
    /// on each item's prefab entity.
    /// Called from CookbookPlugin.OnHeartInitialized() after Heart is ready.
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
        int patched   = 0;
        int failed    = 0;

        foreach (var (itemName, data) in stackEntries)
        {
            if (!PrefabNameResolver.TryResolve(itemName, out PrefabGUID guid))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"Could not resolve item '{itemName}' — not in LilithsMind definitions. Skipping.");
                failed++;
                continue;
            }

            if (!prefabMap.TryGetValue(guid, out var entity))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"Item '{itemName}' (GUID {guid._Value}) has no prefab entity. Skipping.");
                failed++;
                continue;
            }

            if (!entity.Has<ItemData>())
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"'{itemName}' has no ItemData component — cannot set StackSize. Skipping.");
                failed++;
                continue;
            }

            // Read → mutate → write back (value-type struct semantics).
            // [PERFORMANCE] One component read + one write per item at startup.
            var itemData = entity.Read<ItemData>();
            itemData.MaxAmount = data.StackSize!.Value;
            entity.Write(itemData);

            HeartLogger.Info(LOG_SOURCE,
                $"[StackSize] '{itemName}' MaxAmount → {data.StackSize.Value}");
            patched++;
        }

        HeartLogger.Info(LOG_SOURCE,
            $"StackSize patching complete — {patched} patched, {failed} failed.");
    }
}