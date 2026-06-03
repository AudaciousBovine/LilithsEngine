// ============================================================
//  ItemFunctionalService — LilithsHeart
//  LilithsHeart/Services/ItemFunctionalService.cs
//
//  Server-side owner of item functional overrides.
//  Reads LilithItemFunctionalData entries from LilithItemConfig
//  and applies them to ECS prefab entities at world ready.
//
//  Currently owns: StackSize → ItemData.MaxAmount
//  Future fields:  Durability, BloodEssenceValue, Weight, etc.
//
//  Responsibility split:
//  ──────────────────────
//  LocalizationService  owns: DisplayName, DescriptionText
//  InterfaceService     owns: Icon
//  ItemFunctionalService owns: StackSize (and future functional fields)
//
//  Server-side only:
//  ──────────────────
//  Functional overrides are NEVER synced to Soul. Stack size is
//  enforced server-side by the ECS inventory system — the client
//  reads constraints from the server's state, not a local value.
//  Do not add functional fields to ServerSyncPayload.
//
//  ECS approach:
//  ─────────────
//  ItemData.MaxAmount lives on item prefab entities in
//  PrefabCollectionSystem._PrefabGuidToEntityMap. A single write
//  at startup is sufficient — no two-pass needed (item prefabs
//  have no live world instances that get reset by RegisterGameData).
//
//  [PERFORMANCE] Runs once at startup. O(configured items).
//                No per-frame cost.
// ============================================================

using ProjectM;
using Stunlock.Core;
using LilithsHeart.Config;
using LilithsHeart.Foundation;
using LilithsHeart.Services;

namespace LilithsHeart.Services;

public static class ItemFunctionalService
{
    private const string LOG_SOURCE = "LilithsHeart.ItemFunctionalService";

    /// <summary>
    /// Applies all functional overrides from LilithItemConfig to ECS prefab entities.
    /// Called from Heart.OnInitialize() after LocalizationService.Initialize().
    ///
    /// Currently patches:
    ///   StackSize → ItemData.MaxAmount
    /// </summary>
    public static void ApplyOverrides()
    {
        var overrides = LilithItemConfig.FunctionalOverrides;

        if (overrides.Count == 0)
        {
            HeartLogger.Info(LOG_SOURCE, "No item functional overrides configured.");
            return;
        }

        int patched = 0;
        int failed  = 0;

        var prefabMap = Heart.PrefabCollectionSystem._PrefabGuidToEntityMap;

        foreach (var (itemName, data) in overrides)
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
                    $"Item '{itemName}' resolved to GUID {guid._Value} " +
                    "but no prefab entity found. Skipping.");
                failed++;
                continue;
            }

            bool anyApplied = false;

            // ── StackSize → ItemData.MaxAmount ────────────────────────────────
            // Read → mutate → write back (value-type struct semantics).
            // [PERFORMANCE] One component read + one write per item at startup.
            if (data.StackSize.HasValue)
            {
                if (!entity.Has<ItemData>())
                {
                    HeartLogger.Warning(LOG_SOURCE,
                        $"'{itemName}' has no ItemData component — cannot set StackSize. Skipping.");
                    failed++;
                    continue;
                }

                var itemData = entity.Read<ItemData>();
                itemData.MaxAmount = data.StackSize.Value;
                entity.Write(itemData);

                HeartLogger.Info(LOG_SOURCE,
                    $"[StackSize] '{itemName}' MaxAmount → {data.StackSize.Value}");
                anyApplied = true;
            }

            if (anyApplied) patched++;
        }

        HeartLogger.Info(LOG_SOURCE,
            $"Item functional overrides complete — {patched} patched, {failed} failed.");
    }
}