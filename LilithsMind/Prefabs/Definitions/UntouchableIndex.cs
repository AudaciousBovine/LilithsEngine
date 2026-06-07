// ============================================================
//  UntouchableIndex — LilithsMind
//  LilithsMind/Prefabs/Definitions/UntouchableIndex.cs
//
//  Catalogue of recipe prefabs that must never be modified,
//  converted, or assigned a RecipeType by any LilithsEngine module.
//
//  BLOCKLIST CATEGORIES
//  ─────────────────────
//  Tracking — internal engine sentinels explicitly named
//    "DO_NOT_ADD" by Stunlock. Component signature is anomalous
//    (empty RequirementBuffer, ItemRepairBuffer present with no
//    apparent purpose, FakeItem output). Purpose is opaque —
//    they appear to be used internally by the blood tracking
//    and shard bearer tracking systems. Adding them to any
//    crafting station or modifying their buffers risks breaking
//    those systems in undefined ways.
//
//  FusionForge — recipes whose output is resolved dynamically
//    at craft time by the FusionForge system rather than from
//    RecipeOutputBuffer. RecipeOutputBuffer is intentionally
//    empty. Modifying requirements or repair costs is technically
//    safe but the type system does not expose these as a
//    configurable archetype — the forge's dynamic output
//    resolution is opaque and must not be disrupted.
//
//  USAGE
//  ──────
//  RecipeTypePatcher checks IsUntouchable() before processing
//  any recipe entry. If the check returns true, the entry is
//  skipped with a warning and no ECS changes are made.
//
//  [PERFORMANCE] HashSet<int> lookup is O(1). The set is built
//                once at class initialisation from the static
//                field GuidHash values — no per-frame cost.
// ============================================================

using LilithsMind.Prefabs;

namespace LilithsMind.Prefabs.Definitions;

/// <summary>
/// Blocklisted recipe prefabs. No LilithsEngine module should
/// modify, convert, or assign a RecipeType to any entry here.
/// Use IsUntouchable(int guidHash) for O(1) patcher checks.
/// </summary>
public static class UntouchableIndex
{
    // ── Tracking sentinels ────────────────────────────────────────────────────
    // [ADDED] Internal engine recipes explicitly marked DO_NOT_ADD by Stunlock.
    // Anomalous component signature: empty RequirementBuffer, ItemRepairBuffer
    // present (bone stacks, purpose unknown), FakeItem output pointing at
    // FakeItem_Prisoner_ExtractedBloodPotion. Both output the same FakeItem
    // despite representing different tracking systems.

    /// <summary>
    /// Internal blood tracking sentinel. Prefab name contains "DO_NOT_ADD".
    /// Never add to any crafting station or modify any buffer.
    /// </summary>
    public static readonly PrefabDef Recipe_Fake_DO_NOT_ADD_BloodTracking = new()
    {
        GuidHash = -726644851,
        Prefab   = "Recipe_Fake_DO_NOT_ADD_BloodTracking",
    };

    /// <summary>
    /// Internal shard bearer tracking sentinel. Prefab name contains "DO_NOT_ADD".
    /// Never add to any crafting station or modify any buffer.
    /// </summary>
    public static readonly PrefabDef Recipe_Fake_DO_NOT_ADD_ShardBearerTracking = new()
    {
        GuidHash = -1431813390,
        Prefab   = "Recipe_Fake_DO_NOT_ADD_ShardBearerTracking",
    };

    // ── FusionForge recipes ───────────────────────────────────────────────────
    // [ADDED] FusionForge recipes have an intentionally empty RecipeOutputBuffer.
    // Output is resolved dynamically by the FusionForge system at craft time
    // based on the item being fused — not from the buffer. RecipeTypePatcher
    // does not expose FusionForge as a configurable archetype. Requirements
    // and repair costs are technically mutable but are excluded here to prevent
    // accidental disruption of the forge system.

    /// <summary>
    /// FusionForge jewel fusion recipe. Output resolved dynamically at craft time.
    /// RecipeOutputBuffer is intentionally empty — do not populate it.
    /// </summary>
    public static readonly PrefabDef Recipe_FusionForge_FuseJewel = new()
    {
        GuidHash = -664369931,
        Prefab   = "Recipe_FusionForge_FuseJewel",
    };

    /// <summary>
    /// FusionForge weapon fusion recipe. Output resolved dynamically at craft time.
    /// RecipeOutputBuffer is intentionally empty — do not populate it.
    /// </summary>
    public static readonly PrefabDef Recipe_FusionForge_FuseWeapon = new()
    {
        GuidHash = 1716898700,
        Prefab   = "Recipe_FusionForge_FuseWeapon",
    };

    // ── O(1) lookup set ───────────────────────────────────────────────────────
    // [ADDED] Built once from the static fields above. Used by RecipeTypePatcher
    // to check any recipe GUID before performing ECS mutations.
    // [PERFORMANCE] HashSet<int> lookup is O(1). Initialised once at class load.

    private static readonly HashSet<int> _blocklist = new()
    {
        Recipe_Fake_DO_NOT_ADD_BloodTracking.GuidHash,
        Recipe_Fake_DO_NOT_ADD_ShardBearerTracking.GuidHash,
        Recipe_FusionForge_FuseJewel.GuidHash,
        Recipe_FusionForge_FuseWeapon.GuidHash,
    };

    /// <summary>
    /// Returns true if the given GuidHash belongs to a blocklisted recipe.
    /// Call this before any ECS mutation on a recipe entity.
    /// [PERFORMANCE] O(1) HashSet lookup.
    /// </summary>
    public static bool IsUntouchable(int guidHash)
        => _blocklist.Contains(guidHash);

}