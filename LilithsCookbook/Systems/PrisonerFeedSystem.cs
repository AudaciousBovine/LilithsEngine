// ============================================================
//  PrisonerFeedSystem — LilithsCookbook
//  LilithsCookbook/Systems/PrisonerFeedSystem.cs
//
//  Patches FakeItem prefab entities that drive prisoner feeding
//  behaviour. Reads PrisonerFeeding blocks from Recipes/*.json
//  via CookbookPlugin.PrisonerFeedData.
//
//  How this system relates to RecipeSystem:
//  ─────────────────────────────────────────
//  RecipeSystem handles the Recipe_Misc_FeedPrisoner_* prefabs —
//  what real food item is consumed and which FakeItem is output.
//  PrisonerFeedSystem handles the FakeItem prefabs themselves —
//  the stat effects once the FakeItem is "consumed" by the prisoner.
//  Both systems are independent and operate on different entities.
//
//  Lookup approach:
//  ─────────────────
//  FakeItems are standard prefab entities in PrefabGuidToEntityMap.
//  No RecipeHashLookupMap equivalent exists for them.
//  We resolve by name via PrefabNameResolver (Name alias or Prefab
//  string) → PrefabGUID → Entity → component read/write.
//
//  No two-pass required:
//  ──────────────────────
//  FakeItems are never placed as live world instances — they exist
//  only as prefab entities. RegisterGameData() does not reset them.
//  A single prefab patch at startup is sufficient.
//
//  Component dispatch:
//  ───────────────────
//  PrisonerFeedEntryData.Type determines which ECS component to
//  read and write. If the declared Type's component is absent from
//  the entity, the entry is skipped with a warning — this prevents
//  silent corruption if an admin points the wrong Type at a prefab.
//
//  [CHANGED] Full implementation replacing stub. ECS component names
//            confirmed from VampireReferenceAssemblies via dnSpy:
//              ProjectM.FeedPrisoner
//              ProjectM.AffectPrisonerWithToxic
//              ProjectM.DealDamageToPrisoner
//
//  [PERFORMANCE] Runs once at startup. O(configured entries) prefab
//                lookups and component writes. No per-frame cost.
// ============================================================

using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using LilithsHeart.Foundation;
using LilithsHeart.Services;
using LilithsCookbook.Data;

namespace LilithsCookbook.Systems;

public static class PrisonerFeedSystem
{
    private const string LOG_SOURCE = "LilithsCookbook.PrisonerFeedSystem";

    // ── Public entry point ────────────────────────────────────────────────────

    public static void ApplyChanges()
    {
        var config = CookbookPlugin.PrisonerFeedData;

        if (config == null || config.PrisonerFeeding.Count == 0)
        {
            HeartLogger.Info(LOG_SOURCE, "No prisoner feeding changes configured.");
            return;
        }

        int enabled = config.PrisonerFeeding.Count(kvp => kvp.Value.ChangesEnabled);
        if (enabled == 0)
        {
            HeartLogger.Info(LOG_SOURCE,
                "No prisoner feeding entries had ChangesEnabled = true — skipping.");
            return;
        }

        var prefabMap = Heart.PrefabCollectionSystem._PrefabGuidToEntityMap;
        int changed   = 0;
        int failed    = 0;

        foreach (var (fakeItemName, entry) in config.PrisonerFeeding)
        {
            if (!entry.ChangesEnabled) continue;

            // Resolve the FakeItem prefab name → GUID → entity.
            if (!PrefabNameResolver.TryResolve(fakeItemName, out PrefabGUID guid))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"Could not resolve FakeItem '{fakeItemName}' — not in LilithsMind definitions. " +
                    "Add a PrefabDef entry to the appropriate index, or check the prefab name spelling.");
                failed++;
                continue;
            }

            if (!prefabMap.TryGetValue(guid, out Entity entity))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"FakeItem '{fakeItemName}' resolved to GUID {guid._Value} " +
                    "but no prefab entity found — skipping.");
                failed++;
                continue;
            }

            // Dispatch to the correct component patcher based on declared Type.
            bool success = entry.Type switch
            {
                PrisonerFeedTypeEnum.FeedPrisoner         => PatchFeedPrisoner(entity, entry, fakeItemName),
                PrisonerFeedTypeEnum.AffectWithToxic      => PatchAffectWithToxic(entity, entry, fakeItemName),
                PrisonerFeedTypeEnum.DealDamageToPrisoner => PatchDealDamageToPrisoner(entity, entry, fakeItemName),
                _ => LogUnknownType(entry.Type, fakeItemName),
            };

            if (success) changed++;
            else         failed++;
        }

        HeartLogger.Info(LOG_SOURCE,
            $"Prisoner feeding patching complete — {changed} patched, {failed} failed.");
    }

    // ── Component patchers ────────────────────────────────────────────────────

    /// <summary>
    /// Patches ProjectM.FeedPrisoner on a standard food FakeItem.
    /// Handles: fish, gruel, and other normal feed items.
    ///
    /// All fields are nullable — only specified fields are overwritten.
    /// The component is read, fields applied, and written back in full
    /// (value-type struct — partial mutation is a no-op without write-back).
    /// </summary>
    static bool PatchFeedPrisoner(Entity entity, PrisonerFeedEntryData entry, string name)
    {
        if (!entity.Has<FeedPrisoner>())
        {
            HeartLogger.Warning(LOG_SOURCE,
                $"'{name}' declared Type=FeedPrisoner but has no ProjectM.FeedPrisoner " +
                "component — wrong Type? Skipping.");
            return false;
        }

        // Read → mutate → write back (value-type struct semantics).
        var component = entity.Read<FeedPrisoner>();

        if (entry.RecoverHealth_Min.HasValue)       component.RecoverHealth_Min       = entry.RecoverHealth_Min.Value;
        if (entry.RecoverHealth_Max.HasValue)       component.RecoverHealth_Max       = entry.RecoverHealth_Max.Value;
        if (entry.RecoverMisery_Min.HasValue)       component.RecoverMisery_Min       = entry.RecoverMisery_Min.Value;
        if (entry.RecoverMisery_Max.HasValue)       component.RecoverMisery_Max       = entry.RecoverMisery_Max.Value;
        if (entry.AlterBloodQuality_Min.HasValue)   component.AlterBloodQuality_Min   = entry.AlterBloodQuality_Min.Value;
        if (entry.AlterBloodQuality_Max.HasValue)   component.AlterBloodQuality_Max   = entry.AlterBloodQuality_Max.Value;

        entity.Write(component);

        HeartLogger.Info(LOG_SOURCE,
            $"[FeedPrisoner] Patched '{name}': " +
            $"Health=[{component.RecoverHealth_Min:F2}–{component.RecoverHealth_Max:F2}] " +
            $"Misery=[{component.RecoverMisery_Min:F2}–{component.RecoverMisery_Max:F2}] " +
            $"BloodQuality=[{component.AlterBloodQuality_Min:F2}–{component.AlterBloodQuality_Max:F2}]");

        return true;
    }

    /// <summary>
    /// Patches ProjectM.AffectPrisonerWithToxic on a toxic food FakeItem.
    /// Handles: IrradiantGruel and similar mutation-risk items.
    ///
    /// Note: MutantType, SpawnBuff, and BuffSuccess GUIDs are not exposed
    /// in config — changing these would require separate PrefabDef entries
    /// and is out of scope for value-field configuration.
    /// </summary>
    static bool PatchAffectWithToxic(Entity entity, PrisonerFeedEntryData entry, string name)
    {
        if (!entity.Has<AffectPrisonerWithToxic>())
        {
            HeartLogger.Warning(LOG_SOURCE,
                $"'{name}' declared Type=AffectWithToxic but has no " +
                "ProjectM.AffectPrisonerWithToxic component — wrong Type? Skipping.");
            return false;
        }

        var component = entity.Read<AffectPrisonerWithToxic>();

        if (entry.ChanceToBecomeMutant.HasValue)      component.ChanceToBecomeMutant      = entry.ChanceToBecomeMutant.Value;
        if (entry.IncreaseBloodQuality_Min.HasValue)  component.IncreaseBloodQuality_Min  = entry.IncreaseBloodQuality_Min.Value;
        if (entry.IncreaseBloodQuality_Max.HasValue)  component.IncreaseBloodQuality_Max  = entry.IncreaseBloodQuality_Max.Value;

        entity.Write(component);

        HeartLogger.Info(LOG_SOURCE,
            $"[AffectWithToxic] Patched '{name}': " +
            $"MutantChance={component.ChanceToBecomeMutant:F2} " +
            $"BloodQuality=[{component.IncreaseBloodQuality_Min:F4}–{component.IncreaseBloodQuality_Max:F4}]");

        return true;
    }

    /// <summary>
    /// Patches ProjectM.DealDamageToPrisoner on a blood extraction FakeItem.
    /// Handles: ExtractedBloodPotion and similar damage-dealing actions.
    ///
    /// Values are fractional percentages of the prisoner's max Health/Misery.
    /// e.g. DealPercentualDamage_Min=0.3 deals 30% of max health as damage.
    /// </summary>
    static bool PatchDealDamageToPrisoner(Entity entity, PrisonerFeedEntryData entry, string name)
    {
        if (!entity.Has<DealDamageToPrisoner>())
        {
            HeartLogger.Warning(LOG_SOURCE,
                $"'{name}' declared Type=DealDamageToPrisoner but has no " +
                "ProjectM.DealDamageToPrisoner component — wrong Type? Skipping.");
            return false;
        }

        var component = entity.Read<DealDamageToPrisoner>();

        if (entry.DealPercentualDamage_Min.HasValue)   component.DealPercentualDamage_Min   = entry.DealPercentualDamage_Min.Value;
        if (entry.DealPercentualDamage_Max.HasValue)   component.DealPercentualDamage_Max   = entry.DealPercentualDamage_Max.Value;
        if (entry.DealPercentualTorture_Min.HasValue)  component.DealPercentualTorture_Min  = entry.DealPercentualTorture_Min.Value;
        if (entry.DealPercentualTorture_Max.HasValue)  component.DealPercentualTorture_Max  = entry.DealPercentualTorture_Max.Value;

        entity.Write(component);

        HeartLogger.Info(LOG_SOURCE,
            $"[DealDamageToPrisoner] Patched '{name}': " +
            $"Damage=[{component.DealPercentualDamage_Min:F2}–{component.DealPercentualDamage_Max:F2}] " +
            $"Torture=[{component.DealPercentualTorture_Min:F2}–{component.DealPercentualTorture_Max:F2}]");

        return true;
    }

    static bool LogUnknownType(PrisonerFeedTypeEnum type, string name)
    {
        HeartLogger.Warning(LOG_SOURCE,
            $"'{name}' has unknown Type '{type}' — skipping.");
        return false;
    }
}