// ============================================================
//  CookbookPrisonerFeedData — LilithsCookbook
//  LilithsCookbook/Data/CookbookPrisonerFeedData.cs
//
//  DTOs for prisoner feeding / FakeItem configuration.
//
//  Background — how prisoner feeding actually works in ECS:
//  ─────────────────────────────────────────────────────────
//  Each feed action in V Rising has two ECS layers:
//
//  Layer 1 — the Recipe prefab (e.g. Recipe_Misc_FeedPrisoner_Fish_SageFish):
//    • Standard ProjectM.RecipeData + RecipeRequirementBuffer + RecipeOutputBuffer
//    • Requirement: the real food item (e.g. Item_Ingredient_Fish_SageFish_T02)
//    • Output: a FakeItem prefab (e.g. FakeItem_FeedPrisoner_SageFish)
//    • These appear in RecipeHashLookupMap and are handled by RecipeSystem.
//      Admin can change the food item required via normal recipe config.
//
//  Layer 2 — the FakeItem prefab (e.g. FakeItem_FeedPrisoner_SageFish):
//    • NOT a real inventory item. Consumed immediately by the prisoner system.
//    • Carries one of three behaviour components:
//
//      ProjectM.FeedPrisoner          — standard food (fish, gruel, etc.)
//        RecoverHealth_Min/Max        — fractional health recovery range
//        RecoverMisery_Min/Max        — fractional misery recovery range
//        AlterBloodQuality_Min/Max    — blood quality delta range
//        BuffIncreaseBloodQualitySuccess/Fail — buff GUIDs fired on outcome
//
//      ProjectM.AffectPrisonerWithToxic — toxic/irradiant food
//        ChanceToBecomeMutant         — float probability of mutation
//        IncreaseBloodQuality_Min/Max — blood quality delta
//        MutantType                   — GUID of spawned mutant prefab
//        SpawnBuff / BuffSuccess      — buff GUIDs
//
//      ProjectM.DealDamageToPrisoner  — blood extraction
//        DealPercentualDamage_Min/Max — fractional health damage dealt
//        DealPercentualTorture_Min/Max — fractional misery increase
//
//  Config keys are FakeItem prefab names (or LilithsMind Name aliases).
//  A Type discriminator tells PrisonerFeedSystem which component to write.
//
//  The Recipe layer (which food triggers which FakeItem) is configured
//  separately via the normal Recipes block — change the RequirementBuffer
//  on the Recipe_Misc_FeedPrisoner_* entry to change the trigger food.
//
//  [CHANGED] Full rewrite from stub. Previous version used incorrect
//            HealthChange/MiseryChange float fields keyed by feed item.
//            Correct approach: keyed by FakeItem prefab, typed by
//            behaviour component. No PrisonerFeedRecipeData component
//            exists — the feed recipe is standard RecipeData.
//
//  [CHANGED] PrisonerFeedEntryData.Type now carries a [JsonConverter]
//            attribute so System.Text.Json deserializes it from its
//            string name ("FeedPrisoner" etc.) without needing a global
//            JsonStringEnumConverter in CookbookLoader._readOptions.
//            A global converter on .NET 6 silently nulls out nullable
//            value-type fields (float?, bool?, int?) in surrounding
//            objects — scoping it per-field avoids that regression.
//
//  [PERFORMANCE] Plain DTOs — no ECS types, no Unity dependencies.
//                Deserialized once at startup by CookbookLoader.
// ============================================================

using System.Text.Json.Serialization;

namespace LilithsCookbook.Data;

/// <summary>
/// Top-level container for all prisoner FakeItem config entries.
/// Lives alongside CookbookRecipeData.Recipes in the same JSON files
/// under the "PrisonerFeeding" key.
///
/// Keys are FakeItem prefab names (or LilithsMind Name aliases).
/// e.g. "FakeItem_FeedPrisoner_SageFish", "FakeItem_FeedPrisoner_IrradiantGruel"
///
/// To change what food item triggers a feed action, use the normal
/// Recipes block to modify the RecipeRequirementBuffer on the
/// Recipe_Misc_FeedPrisoner_* entry instead.
/// </summary>
public class CookbookPrisonerFeedData
{
    public Dictionary<string, PrisonerFeedEntryData> PrisonerFeeding { get; set; } = new();
}

/// <summary>
/// Configuration record for a single FakeItem prefab.
/// The Type field determines which ECS component is read and written.
/// Only fields relevant to the declared Type need to be specified.
/// All value fields are nullable — null means keep vanilla value.
/// </summary>
public class PrisonerFeedEntryData
{
    /// <summary>
    /// When false this entry is skipped entirely.
    /// Set to true to activate changes for this FakeItem.
    /// </summary>
    public bool ChangesEnabled { get; set; } = false;

    /// <summary>
    /// Discriminator — determines which ECS component to patch.
    ///   FeedPrisoner         → ProjectM.FeedPrisoner
    ///   AffectWithToxic      → ProjectM.AffectPrisonerWithToxic
    ///   DealDamageToPrisoner → ProjectM.DealDamageToPrisoner
    /// Must match the component actually present on the FakeItem prefab.
    ///
    /// [CHANGED] [JsonConverter] applied here rather than registering
    ///           JsonStringEnumConverter globally in CookbookLoader.
    ///           Global registration on .NET 6 silently nulls out
    ///           nullable value-type fields (float?, bool?, int?) in
    ///           surrounding deserialized objects.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PrisonerFeedTypeEnum Type { get; set; } = PrisonerFeedTypeEnum.FeedPrisoner;

    // ── FeedPrisoner fields (ProjectM.FeedPrisoner) ───────────────────────────
    // Used when Type = FeedPrisoner.
    // All values are fractional (0.0–1.0) representing a percentage of
    // the prisoner's max stat.

    /// <summary>Minimum fractional health recovery. Null = keep vanilla.</summary>
    public float? RecoverHealth_Min { get; set; }

    /// <summary>Maximum fractional health recovery. Null = keep vanilla.</summary>
    public float? RecoverHealth_Max { get; set; }

    /// <summary>Minimum fractional misery recovery (reduction). Null = keep vanilla.</summary>
    public float? RecoverMisery_Min { get; set; }

    /// <summary>Maximum fractional misery recovery (reduction). Null = keep vanilla.</summary>
    public float? RecoverMisery_Max { get; set; }

    /// <summary>Minimum blood quality change. Negative = decrease. Null = keep vanilla.</summary>
    public float? AlterBloodQuality_Min { get; set; }

    /// <summary>Maximum blood quality change. Negative = decrease. Null = keep vanilla.</summary>
    public float? AlterBloodQuality_Max { get; set; }

    // ── AffectWithToxic fields (ProjectM.AffectPrisonerWithToxic) ────────────
    // Used when Type = AffectWithToxic.

    /// <summary>Probability (0.0–1.0) the prisoner becomes a mutant. Null = keep vanilla.</summary>
    public float? ChanceToBecomeMutant { get; set; }

    /// <summary>Minimum blood quality increase from toxic feed. Null = keep vanilla.</summary>
    public float? IncreaseBloodQuality_Min { get; set; }

    /// <summary>Maximum blood quality increase from toxic feed. Null = keep vanilla.</summary>
    public float? IncreaseBloodQuality_Max { get; set; }

    // ── DealDamageToPrisoner fields (ProjectM.DealDamageToPrisoner) ───────────
    // Used when Type = DealDamageToPrisoner.
    // Values are fractional (0.0–1.0) of the prisoner's max Health/Misery.

    /// <summary>Minimum fractional health damage dealt to prisoner. Null = keep vanilla.</summary>
    public float? DealPercentualDamage_Min { get; set; }

    /// <summary>Maximum fractional health damage dealt to prisoner. Null = keep vanilla.</summary>
    public float? DealPercentualDamage_Max { get; set; }

    /// <summary>Minimum fractional misery increase dealt to prisoner. Null = keep vanilla.</summary>
    public float? DealPercentualTorture_Min { get; set; }

    /// <summary>Maximum fractional misery increase dealt to prisoner. Null = keep vanilla.</summary>
    public float? DealPercentualTorture_Max { get; set; }
}

/// <summary>
/// Identifies which ECS behaviour component lives on a FakeItem prefab.
/// Must match the component actually present — mismatching Type and component
/// will log a warning and skip the entry.
/// </summary>
public enum PrisonerFeedTypeEnum
{
    /// <summary>
    /// Standard food feeding. Patches ProjectM.FeedPrisoner.
    /// e.g. FakeItem_FeedPrisoner_SageFish, FakeItem_FeedPrisoner_TwilightSnapper
    /// </summary>
    FeedPrisoner,

    /// <summary>
    /// Toxic / irradiant food. Patches ProjectM.AffectPrisonerWithToxic.
    /// e.g. FakeItem_FeedPrisoner_IrradiantGruel
    /// </summary>
    AffectWithToxic,

    /// <summary>
    /// Blood extraction. Patches ProjectM.DealDamageToPrisoner.
    /// e.g. FakeItem_Prisoner_ExtractedBloodPotion
    /// </summary>
    DealDamageToPrisoner,
}