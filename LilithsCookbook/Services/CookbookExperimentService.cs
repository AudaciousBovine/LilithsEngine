// ============================================================
//  CookbookExperimentService — LilithsCookbook
//  LilithsCookbook/Services/CookbookExperimentService.cs
//
//  Experimental ECS component add/remove tests.
//  These tests probe whether structural archetype mutations
//  (adding or removing IComponentData from a prefab entity at
//  startup) are safe in V Rising's ECS world.
//
//  WHY THIS MATTERS
//  ─────────────────
//  We already add/remove DynamicBuffers (AddBuffer, RemoveBuffer)
//  and that works fine. But adding or removing a structural
//  IComponentData component moves the entity to a NEW archetype
//  chunk in ECS memory. The question is whether V Rising's
//  PrefabCollectionSystem, RegisterGameData(), the network layer,
//  or any other system objects to that chunk reallocation at
//  startup on a prefab entity.
//
//  If this works, it unlocks a RecipeType system where any recipe
//  can be converted to fill any role by injecting or stripping
//  the relevant behaviour components.
//
//  TEST A — Add a structural component to a FakeItem prefab
//  ─────────────────────────────────────────────────────────
//  Target: FakeItem_FeedPrisoner_Rat (PrefabGUID 1110550218)
//  Action: Add ProjectM.AffectPrisonerWithToxic alongside the
//          existing ProjectM.FeedPrisoner.
//  Also:   Remove ProjectM.ConsumableCondition (zero-size tag) as
//          a secondary purely-structural test with no semantic cost.
//  Verify: Component exists post-add, canary value survives write-back,
//          entity still valid, prefab map still resolves it.
//  Risk:   This entity carries Network.* components (Networked,
//          NetworkId, NetworkSnapshot, etc.) — archetype mutation on
//          a networked prefab may be more sensitive than on a bare one.
//
//  TEST B — Remove a structural component from a recipe prefab
//  ────────────────────────────────────────────────────────────
//  Target: Recipe_Weapon_Sword_T01_Bone (PrefabGUID -2125590443)
//  Action: Remove ProjectM.RecipeOutputUnitBuffer (present but empty
//          on this entity — zero data loss, purely archetype change).
//  Verify: Entity still valid, RecipeData still readable,
//          RecipeHashLookupMap still resolves the GUID after removal.
//  Risk:   Lower than Test A — this entity has NO network components.
//          RecipeOutputUnitBuffer is a DynamicBuffer, so this is the
//          same path as our existing UseUnitOutputs=false removal, but
//          done explicitly here to confirm the mechanic in isolation.
//
//  READING THE RESULTS
//  ────────────────────
//  All output is prefixed [EXPERIMENT] for easy log grepping.
//  Look for:
//    [EXPERIMENT][PASS]  — operation succeeded and read-back confirmed
//    [EXPERIMENT][FAIL]  — operation failed or read-back was wrong
//    [EXPERIMENT][SKIP]  — precondition not met (entity/component absent)
//    [EXPERIMENT][ERROR] — exception thrown
//
//  [PERFORMANCE] Runs once at startup, only when flags are true.
//                Zero cost when both flags are false.
// ============================================================

using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using LilithsHeart.Foundation;
using LilithsHeart.Services;
using LilithsCookbook.Config;

namespace LilithsCookbook.Services;

public static class CookbookExperimentService
{
    private const string LOG_SOURCE = "LilithsCookbook.CookbookExperimentService";

    // ── Known GUIDs (from component dumps) ───────────────────────────────────

    // [ADDED] FakeItem_FeedPrisoner_Rat — carries FeedPrisoner, InventoryItem,
    //         ItemData, ConsumableCondition, and Network.* components.
    //         PrefabGUID confirmed from component dump file.
    private static readonly PrefabGUID FakeItemRatGuid = new(1110550218);

    // [ADDED] Recipe_Weapon_Sword_T01_Bone — carries RecipeData, all recipe
    //         buffers (including empty RecipeOutputUnitBuffer), no network
    //         components. PrefabGUID confirmed from component dump file.
    private static readonly PrefabGUID RecipeBoneSwordGuid = new(-2125590443);

    // ── Public entry points ───────────────────────────────────────────────────

    /// <summary>
    /// Test A — attempts to add ProjectM.AffectPrisonerWithToxic and remove
    /// ProjectM.ConsumableCondition from FakeItem_FeedPrisoner_Rat.
    ///
    /// Called from CookbookPlugin.OnHeartInitialized() when
    /// CookbookConfig.RunComponentAddTest = true.
    /// Auto-resets the flag after running.
    /// </summary>
    public static void RunAddTest()
    {
        HeartLogger.Info(LOG_SOURCE,
            "[EXPERIMENT] ── TEST A: Component ADD on FakeItem_FeedPrisoner_Rat ──────────");
        HeartLogger.Info(LOG_SOURCE,
            "[EXPERIMENT] Target: FakeItem_FeedPrisoner_Rat (GUID 1110550218)");
        HeartLogger.Info(LOG_SOURCE,
            "[EXPERIMENT] Add: ProjectM.AffectPrisonerWithToxic (not present by default)");
        HeartLogger.Info(LOG_SOURCE,
            "[EXPERIMENT] Remove: ProjectM.ConsumableCondition (zero-size tag)");

        try
        {
            var prefabMap = Heart.PrefabCollectionSystem._PrefabGuidToEntityMap;
            var em        = Heart.EntityManager;

            // ── Step 1: Resolve entity ────────────────────────────────────────

            if (!prefabMap.TryGetValue(FakeItemRatGuid, out Entity entity))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    "[EXPERIMENT][SKIP] FakeItem_FeedPrisoner_Rat not found in prefab map. " +
                    "Ensure LilithsMind has an entry for this prefab, or the GUID is correct.");
                CookbookConfig.DisableRunComponentAddTest();
                return;
            }

            HeartLogger.Info(LOG_SOURCE,
                $"[EXPERIMENT] Entity resolved: Index={entity.Index} Version={entity.Version}");

            // ── Step 2: Log baseline component presence ───────────────────────

            LogComponentPresence(em, entity, "BASELINE");

            // ── Step 3: Read and log existing FeedPrisoner values ─────────────

            if (!em.HasComponent<FeedPrisoner>(entity))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    "[EXPERIMENT][SKIP] FeedPrisoner component absent — unexpected state. Aborting.");
                CookbookConfig.DisableRunComponentAddTest();
                return;
            }

            var baselineFeed = em.GetComponentData<FeedPrisoner>(entity);
            HeartLogger.Info(LOG_SOURCE,
                $"[EXPERIMENT] Baseline FeedPrisoner: " +
                $"Health=[{baselineFeed.RecoverHealth_Min:F4}–{baselineFeed.RecoverHealth_Max:F4}] " +
                $"Misery=[{baselineFeed.RecoverMisery_Min:F4}–{baselineFeed.RecoverMisery_Max:F4}] " +
                $"BloodQuality=[{baselineFeed.AlterBloodQuality_Min:F4}–{baselineFeed.AlterBloodQuality_Max:F4}]");

            // ── Step 4: Attempt ADD AffectPrisonerWithToxic ───────────────────
            // [ADDED] This is the core experiment — adding a structural IComponentData
            // to a networked prefab entity. If this crashes or corrupts the entity,
            // we know archetype mutation on networked prefabs is not safe.

            HeartLogger.Info(LOG_SOURCE,
                "[EXPERIMENT] Attempting em.AddComponent<AffectPrisonerWithToxic>(entity)...");

            try
            {
                em.AddComponent<AffectPrisonerWithToxic>(entity);
                HeartLogger.Info(LOG_SOURCE,
                    "[EXPERIMENT] AddComponent call completed without exception.");
            }
            catch (Exception ex)
            {
                HeartLogger.Error(LOG_SOURCE,
                    $"[EXPERIMENT][ERROR] AddComponent<AffectPrisonerWithToxic> threw: {ex.GetType().Name}: {ex.Message}");
                CookbookConfig.DisableRunComponentAddTest();
                return;
            }

            // ── Step 5: Verify AffectPrisonerWithToxic now present ────────────

            bool hasAfterAdd = em.HasComponent<AffectPrisonerWithToxic>(entity);
            HeartLogger.Info(LOG_SOURCE,
                hasAfterAdd
                    ? "[EXPERIMENT][PASS] HasComponent<AffectPrisonerWithToxic> = true after add."
                    : "[EXPERIMENT][FAIL] HasComponent<AffectPrisonerWithToxic> = false after add — add had no effect.");

            // ── Step 6: Write canary values and read back ─────────────────────
            // [ADDED] We write recognisable sentinel values (0.42f / 0.88f) to confirm
            // the component is not just present but actually writable and readable.

            if (hasAfterAdd)
            {
                HeartLogger.Info(LOG_SOURCE,
                    "[EXPERIMENT] Writing canary values to AffectPrisonerWithToxic: " +
                    "ChanceToBecomeMutant=0.42, BloodQuality_Min=0.11, BloodQuality_Max=0.88");

                try
                {
                    // Read default values first for the record.
                    var defaultToxic = em.GetComponentData<AffectPrisonerWithToxic>(entity);
                    HeartLogger.Info(LOG_SOURCE,
                        $"[EXPERIMENT] Default AffectPrisonerWithToxic after add: " +
                        $"ChanceToBecomeMutant={defaultToxic.ChanceToBecomeMutant:F4} " +
                        $"BloodQuality=[{defaultToxic.IncreaseBloodQuality_Min:F4}–{defaultToxic.IncreaseBloodQuality_Max:F4}]");

                    // Write canary values.
                    var canary = defaultToxic;
                    canary.ChanceToBecomeMutant     = 0.42f;
                    canary.IncreaseBloodQuality_Min = 0.11f;
                    canary.IncreaseBloodQuality_Max = 0.88f;
                    em.SetComponentData(entity, canary);

                    // Read back immediately.
                    var readBack = em.GetComponentData<AffectPrisonerWithToxic>(entity);
                    bool canaryMatch =
                        MathF.Abs(readBack.ChanceToBecomeMutant     - 0.42f) < 0.0001f &&
                        MathF.Abs(readBack.IncreaseBloodQuality_Min - 0.11f) < 0.0001f &&
                        MathF.Abs(readBack.IncreaseBloodQuality_Max - 0.88f) < 0.0001f;

                    HeartLogger.Info(LOG_SOURCE,
                        canaryMatch
                            ? "[EXPERIMENT][PASS] Canary read-back matched — write+read on added component works."
                            : $"[EXPERIMENT][FAIL] Canary mismatch — got: " +
                              $"ChanceToBecomeMutant={readBack.ChanceToBecomeMutant:F4} " +
                              $"BloodQuality=[{readBack.IncreaseBloodQuality_Min:F4}–{readBack.IncreaseBloodQuality_Max:F4}]");
                }
                catch (Exception ex)
                {
                    HeartLogger.Error(LOG_SOURCE,
                        $"[EXPERIMENT][ERROR] Write/read-back of AffectPrisonerWithToxic threw: " +
                        $"{ex.GetType().Name}: {ex.Message}");
                }
            }

            // ── Step 7: Verify FeedPrisoner still intact after archetype change ─
            // [ADDED] Critical check — confirms the archetype move didn't corrupt
            // or drop the component that was already on the entity.

            bool feedStillPresent = em.HasComponent<FeedPrisoner>(entity);
            HeartLogger.Info(LOG_SOURCE,
                feedStillPresent
                    ? "[EXPERIMENT][PASS] FeedPrisoner still present after archetype change."
                    : "[EXPERIMENT][FAIL] FeedPrisoner LOST after archetype change — data corruption.");

            if (feedStillPresent)
            {
                var postFeed = em.GetComponentData<FeedPrisoner>(entity);
                bool feedIntact =
                    MathF.Abs(postFeed.RecoverHealth_Min - baselineFeed.RecoverHealth_Min) < 0.0001f &&
                    MathF.Abs(postFeed.RecoverHealth_Max - baselineFeed.RecoverHealth_Max) < 0.0001f;

                HeartLogger.Info(LOG_SOURCE,
                    feedIntact
                        ? "[EXPERIMENT][PASS] FeedPrisoner values unchanged after archetype change."
                        : $"[EXPERIMENT][FAIL] FeedPrisoner values drifted after archetype change. " +
                          $"Was: Health=[{baselineFeed.RecoverHealth_Min:F4}–{baselineFeed.RecoverHealth_Max:F4}] " +
                          $"Now: Health=[{postFeed.RecoverHealth_Min:F4}–{postFeed.RecoverHealth_Max:F4}]");
            }

            // ── Step 8: Attempt REMOVE ConsumableCondition (zero-size tag) ───
            // [ADDED] Secondary test — removes a zero-size tag component.
            // This is the simplest possible archetype mutation with no data loss.
            // If even this fails on a networked entity, add is likely also unsafe.

            HeartLogger.Info(LOG_SOURCE,
                "[EXPERIMENT] Attempting em.RemoveComponent<ConsumableCondition>(entity)...");

            bool hadConsumable = em.HasComponent<ConsumableCondition>(entity);
            HeartLogger.Info(LOG_SOURCE,
                $"[EXPERIMENT] ConsumableCondition present before remove: {hadConsumable}");

            if (hadConsumable)
            {
                try
                {
                    em.RemoveComponent<ConsumableCondition>(entity);
                    bool goneAfterRemove = !em.HasComponent<ConsumableCondition>(entity);
                    HeartLogger.Info(LOG_SOURCE,
                        goneAfterRemove
                            ? "[EXPERIMENT][PASS] ConsumableCondition removed successfully."
                            : "[EXPERIMENT][FAIL] ConsumableCondition still present after RemoveComponent.");
                }
                catch (Exception ex)
                {
                    HeartLogger.Error(LOG_SOURCE,
                        $"[EXPERIMENT][ERROR] RemoveComponent<ConsumableCondition> threw: " +
                        $"{ex.GetType().Name}: {ex.Message}");
                }
            }
            else
            {
                HeartLogger.Warning(LOG_SOURCE,
                    "[EXPERIMENT][SKIP] ConsumableCondition not present — skipping remove test. " +
                    "May have already been removed by a prior run.");
            }

            // ── Step 9: Final entity state ────────────────────────────────────

            LogComponentPresence(em, entity, "FINAL");

            // Verify entity is still valid and prefab map still resolves it.
            bool stillInMap = prefabMap.TryGetValue(FakeItemRatGuid, out Entity mapEntity);
            bool mapEntityMatch = stillInMap && mapEntity.Index == entity.Index;

            HeartLogger.Info(LOG_SOURCE,
                stillInMap
                    ? $"[EXPERIMENT][PASS] Prefab map still resolves FakeItemRatGuid. " +
                      $"Entity match: {mapEntityMatch}"
                    : "[EXPERIMENT][FAIL] Prefab map no longer resolves FakeItemRatGuid after mutations.");

            HeartLogger.Info(LOG_SOURCE,
                "[EXPERIMENT] ── TEST A COMPLETE ────────────────────────────────────────");
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE,
                $"[EXPERIMENT][ERROR] Test A outer exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            CookbookConfig.DisableRunComponentAddTest();
        }
    }

    /// <summary>
    /// Test B — attempts to remove ProjectM.RecipeOutputUnitBuffer from
    /// Recipe_Weapon_Sword_T01_Bone.
    ///
    /// RecipeOutputUnitBuffer is present on this entity but EMPTY (confirmed
    /// from the component dump) — this is a pure archetype test with no data
    /// loss. We then verify RecipeData is still readable and the recipe still
    /// resolves in RecipeHashLookupMap.
    ///
    /// Called from CookbookPlugin.OnHeartInitialized() when
    /// CookbookConfig.RunComponentRemoveTest = true.
    /// Auto-resets the flag after running.
    ///
    /// NOTE: Must be called AFTER RecipeSystem.ApplyMapValues() so
    /// RecipeHashLookupMap contains the final patched state when we probe it.
    /// </summary>
    public static void RunRemoveTest()
    {
        HeartLogger.Info(LOG_SOURCE,
            "[EXPERIMENT] ── TEST B: Component REMOVE on Recipe_Weapon_Sword_T01_Bone ───");
        HeartLogger.Info(LOG_SOURCE,
            "[EXPERIMENT] Target: Recipe_Weapon_Sword_T01_Bone (GUID -2125590443)");
        HeartLogger.Info(LOG_SOURCE,
            "[EXPERIMENT] Remove: ProjectM.RecipeOutputUnitBuffer (present but empty)");

        try
        {
            var prefabMap   = Heart.PrefabCollectionSystem._PrefabGuidToEntityMap;
            var recipeMap   = Heart.GameDataSystem.RecipeHashLookupMap;
            var em          = Heart.EntityManager;

            // ── Step 1: Resolve entity ────────────────────────────────────────

            if (!prefabMap.TryGetValue(RecipeBoneSwordGuid, out Entity entity))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    "[EXPERIMENT][SKIP] Recipe_Weapon_Sword_T01_Bone not found in prefab map.");
                CookbookConfig.DisableRunComponentRemoveTest();
                return;
            }

            HeartLogger.Info(LOG_SOURCE,
                $"[EXPERIMENT] Entity resolved: Index={entity.Index} Version={entity.Version}");

            // ── Step 2: Log baseline state ────────────────────────────────────

            LogComponentPresence(em, entity, "BASELINE");

            // Read and log baseline RecipeData (this is what we must protect).
            if (!em.HasComponent<RecipeData>(entity))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    "[EXPERIMENT][SKIP] RecipeData absent on recipe entity — unexpected. Aborting.");
                CookbookConfig.DisableRunComponentRemoveTest();
                return;
            }

            var baselineRecipe = em.GetComponentData<RecipeData>(entity);
            HeartLogger.Info(LOG_SOURCE,
                $"[EXPERIMENT] Baseline RecipeData: " +
                $"CraftDuration={baselineRecipe.CraftDuration:F2} " +
                $"AlwaysUnlocked={baselineRecipe.AlwaysUnlocked} " +
                $"HideInStation={baselineRecipe.HideInStation}");

            // Log baseline RecipeHashLookupMap entry for comparison.
            bool inMapBefore = recipeMap.TryGetValue(RecipeBoneSwordGuid, out var mapEntryBefore);
            HeartLogger.Info(LOG_SOURCE,
                inMapBefore
                    ? $"[EXPERIMENT] Baseline RecipeHashLookupMap entry: " +
                      $"CraftDuration={mapEntryBefore.CraftDuration:F2} " +
                      $"AlwaysUnlocked={mapEntryBefore.AlwaysUnlocked}"
                    : "[EXPERIMENT] WARNING: Recipe not found in RecipeHashLookupMap before remove.");

            // Log buffer presence and sizes for full picture.
            LogBufferPresence(em, entity, "BASELINE");

            // ── Step 3: Confirm RecipeOutputUnitBuffer is empty ───────────────
            // [ADDED] Safety check — we should never remove a buffer with data.
            // The dump confirms it's empty, but verify at runtime to be safe.

            bool hasUnitBuffer = em.HasBuffer<RecipeOutputUnitBuffer>(entity);
            HeartLogger.Info(LOG_SOURCE,
                $"[EXPERIMENT] RecipeOutputUnitBuffer present: {hasUnitBuffer}");

            if (hasUnitBuffer)
            {
                var unitBuf = em.GetBuffer<RecipeOutputUnitBuffer>(entity);
                HeartLogger.Info(LOG_SOURCE,
                    $"[EXPERIMENT] RecipeOutputUnitBuffer length: {unitBuf.Length} (expected 0)");

                if (unitBuf.Length > 0)
                {
                    HeartLogger.Warning(LOG_SOURCE,
                        "[EXPERIMENT][SKIP] RecipeOutputUnitBuffer has data — will not remove " +
                        "to avoid data loss. Confirm this is the right test target.");
                    CookbookConfig.DisableRunComponentRemoveTest();
                    return;
                }
            }
            else
            {
                HeartLogger.Warning(LOG_SOURCE,
                    "[EXPERIMENT][SKIP] RecipeOutputUnitBuffer not present on entity. " +
                    "May have been removed by a prior run or UseUnitOutputs=false config.");
                CookbookConfig.DisableRunComponentRemoveTest();
                return;
            }

            // ── Step 4: Attempt REMOVE RecipeOutputUnitBuffer ─────────────────
            // [ADDED] This is a DynamicBuffer removal — same path as our existing
            // UseUnitOutputs=false code, but done explicitly here on a known-clean
            // entity to isolate and confirm the mechanic. The key question is whether
            // removing a buffer from a prefab entity breaks RecipeHashLookupMap
            // resolution or RecipeData integrity.

            HeartLogger.Info(LOG_SOURCE,
                "[EXPERIMENT] Attempting em.RemoveComponent<RecipeOutputUnitBuffer>(entity)...");

            try
            {
                em.RemoveComponent<RecipeOutputUnitBuffer>(entity);
                HeartLogger.Info(LOG_SOURCE,
                    "[EXPERIMENT] RemoveComponent call completed without exception.");
            }
            catch (Exception ex)
            {
                HeartLogger.Error(LOG_SOURCE,
                    $"[EXPERIMENT][ERROR] RemoveComponent<RecipeOutputUnitBuffer> threw: " +
                    $"{ex.GetType().Name}: {ex.Message}");
                CookbookConfig.DisableRunComponentRemoveTest();
                return;
            }

            // ── Step 5: Verify buffer is gone ─────────────────────────────────

            bool goneAfter = !em.HasBuffer<RecipeOutputUnitBuffer>(entity);
            HeartLogger.Info(LOG_SOURCE,
                goneAfter
                    ? "[EXPERIMENT][PASS] RecipeOutputUnitBuffer no longer present after remove."
                    : "[EXPERIMENT][FAIL] RecipeOutputUnitBuffer still present after remove.");

            // ── Step 6: Verify RecipeData still readable and intact ───────────
            // [ADDED] Confirms archetype migration didn't corrupt the structural
            // component we care most about preserving.

            bool recipeDataPresent = em.HasComponent<RecipeData>(entity);
            HeartLogger.Info(LOG_SOURCE,
                recipeDataPresent
                    ? "[EXPERIMENT][PASS] RecipeData still present after buffer remove."
                    : "[EXPERIMENT][FAIL] RecipeData GONE after buffer remove — archetype corruption.");

            if (recipeDataPresent)
            {
                var postRecipe = em.GetComponentData<RecipeData>(entity);
                bool recipeIntact =
                    MathF.Abs(postRecipe.CraftDuration - baselineRecipe.CraftDuration) < 0.0001f &&
                    postRecipe.AlwaysUnlocked == baselineRecipe.AlwaysUnlocked &&
                    postRecipe.HideInStation  == baselineRecipe.HideInStation;

                HeartLogger.Info(LOG_SOURCE,
                    recipeIntact
                        ? "[EXPERIMENT][PASS] RecipeData values intact after buffer remove."
                        : $"[EXPERIMENT][FAIL] RecipeData values drifted. " +
                          $"Was: CraftDuration={baselineRecipe.CraftDuration:F2} " +
                          $"Now: CraftDuration={postRecipe.CraftDuration:F2}");
            }

            // ── Step 7: Verify RecipeHashLookupMap still resolves ─────────────
            // [ADDED] This is the most important post-remove check. If the map
            // stops resolving the GUID after a buffer remove, the crafting system
            // will break for this recipe. We check both map presence and value.

            bool inMapAfter = recipeMap.TryGetValue(RecipeBoneSwordGuid, out var mapEntryAfter);
            HeartLogger.Info(LOG_SOURCE,
                inMapAfter
                    ? $"[EXPERIMENT][PASS] RecipeHashLookupMap still resolves after remove. " +
                      $"CraftDuration={mapEntryAfter.CraftDuration:F2} " +
                      $"(was {(inMapBefore ? mapEntryBefore.CraftDuration.ToString("F2") : "N/A")})"
                    : "[EXPERIMENT][FAIL] RecipeHashLookupMap NO LONGER resolves after buffer remove.");

            // ── Step 8: Verify remaining recipe buffers still intact ──────────

            LogBufferPresence(em, entity, "FINAL");

            // ── Step 9: Final entity state ────────────────────────────────────

            bool stillInMap = prefabMap.TryGetValue(RecipeBoneSwordGuid, out Entity mapEntity);
            bool mapEntityMatch = stillInMap && mapEntity.Index == entity.Index;

            HeartLogger.Info(LOG_SOURCE,
                stillInMap
                    ? $"[EXPERIMENT][PASS] Prefab map still resolves RecipeBoneSwordGuid. " +
                      $"Entity match: {mapEntityMatch}"
                    : "[EXPERIMENT][FAIL] Prefab map no longer resolves RecipeBoneSwordGuid.");

            HeartLogger.Info(LOG_SOURCE,
                "[EXPERIMENT] ── TEST B COMPLETE ────────────────────────────────────────");
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE,
                $"[EXPERIMENT][ERROR] Test B outer exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            CookbookConfig.DisableRunComponentRemoveTest();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Logs presence of the key structural components and network components
    /// on the FakeItem entity. Called before and after mutations.
    /// </summary>
    static void LogComponentPresence(EntityManager em, Entity entity, string phase)
    {
        // FakeItem-specific components
        bool hasFeedPrisoner       = em.HasComponent<FeedPrisoner>(entity);
        bool hasAffectWithToxic    = em.HasComponent<AffectPrisonerWithToxic>(entity);
        bool hasDealDamage         = em.HasComponent<DealDamageToPrisoner>(entity);
        bool hasConsumable         = em.HasComponent<ConsumableCondition>(entity);
        bool hasItemData           = em.HasComponent<ItemData>(entity);
        bool hasInventoryItem      = em.HasComponent<InventoryItem>(entity);

        // Recipe-specific components
        bool hasRecipeData         = em.HasComponent<RecipeData>(entity);

        // Network components (only relevant for FakeItem)
        bool hasNetworked          = em.HasComponent<ProjectM.Network.Networked>(entity);
        bool hasNetworkId          = em.HasComponent<ProjectM.Network.NetworkId>(entity);

        HeartLogger.Info(LOG_SOURCE,
            $"[EXPERIMENT] [{phase}] Components — " +
            $"FeedPrisoner={hasFeedPrisoner} " +
            $"AffectWithToxic={hasAffectWithToxic} " +
            $"DealDamageToPrisoner={hasDealDamage} " +
            $"ConsumableCondition={hasConsumable} " +
            $"ItemData={hasItemData} " +
            $"InventoryItem={hasInventoryItem} " +
            $"RecipeData={hasRecipeData} " +
            $"Networked={hasNetworked} " +
            $"NetworkId={hasNetworkId}");
    }

    /// <summary>
    /// Logs presence and length of key DynamicBuffers on the recipe entity.
    /// Called before and after mutations for Test B.
    /// </summary>
    static void LogBufferPresence(EntityManager em, Entity entity, string phase)
    {
        bool hasReq     = em.HasBuffer<RecipeRequirementBuffer>(entity);
        bool hasOut     = em.HasBuffer<RecipeOutputBuffer>(entity);
        bool hasRepair  = em.HasBuffer<ItemRepairBuffer>(entity);
        bool hasUnit    = em.HasBuffer<RecipeOutputUnitBuffer>(entity);

        int reqLen    = hasReq    ? em.GetBuffer<RecipeRequirementBuffer>(entity).Length : -1;
        int outLen    = hasOut    ? em.GetBuffer<RecipeOutputBuffer>(entity).Length      : -1;
        int repairLen = hasRepair ? em.GetBuffer<ItemRepairBuffer>(entity).Length        : -1;
        int unitLen   = hasUnit   ? em.GetBuffer<RecipeOutputUnitBuffer>(entity).Length  : -1;

        HeartLogger.Info(LOG_SOURCE,
            $"[EXPERIMENT] [{phase}] Buffers — " +
            $"RequirementBuffer={hasReq}(len={reqLen}) " +
            $"OutputBuffer={hasOut}(len={outLen}) " +
            $"ItemRepairBuffer={hasRepair}(len={repairLen}) " +
            $"UnitOutputBuffer={hasUnit}(len={unitLen})");
    }
}
