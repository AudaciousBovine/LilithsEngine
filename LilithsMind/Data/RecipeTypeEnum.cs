// ============================================================
//  RecipeTypeEnum — LilithsMind
//  LilithsMind/Enums/RecipeTypeEnum.cs
//
//  Declares the two recipe archetypes that the RecipeTypePatcher
//  in LilithsCookbook can convert between.
//
//  BACKGROUND
//  ───────────
//  Analysis of 40+ recipe component dumps revealed that all recipe
//  prefab entities share the same structural component set:
//
//    RecipeData                 — scalar fields (CraftDuration etc.)
//    RecipeRequirementBuffer    — ingredient GUIDs + amounts
//    RecipeOutputBuffer         — output item GUIDs + amounts
//    RecipeOutputUnitBuffer     — unit GUID + stacks (empty on most recipes)
//    DestroyData / DestroyState — universal lifecycle components
//    Transform components       — universal positional components
//    SpawnTag / Prefab / Simulate — universal ECS tags
//
//  The ONLY structural difference between recipe archetypes is the
//  presence or absence of ItemRepairBuffer. Every other variation
//  (what GUIDs are in the output buffer, whether the unit buffer is
//  populated, whether outputs are FakeItems) is purely data — no
//  component add or remove is needed.
//
//  This means the RecipeTypePatcher has exactly one job:
//    Craft          → remove ItemRepairBuffer if present
//    CraftRepairable → add ItemRepairBuffer if absent, populate it
//
//  WHAT THIS DOES NOT COVER
//  ─────────────────────────
//  Recipes in UntouchableIndex are excluded from the type system
//  entirely. Attempting to assign a RecipeType to an untouchable
//  recipe is refused by the patcher with a warning.
//
//  ProgressionGated recipes (those carrying ProgressionDependencyElement)
//  are not exposed as a type — the patcher detects the buffer silently
//  and treats the recipe as Craft for conversion purposes, but never
//  touches ProgressionDependencyElement under any circumstances.
//
//  [PERFORMANCE] Enum value — zero runtime cost.
// ============================================================

using System.Text.Json.Serialization;

namespace LilithsMind;

/// <summary>
/// Declares the recipe archetype an admin wants a recipe to behave as.
/// The RecipeTypePatcher in LilithsCookbook reads this value and performs
/// the minimum ECS structural change needed to match the declared type —
/// which in practice means adding or removing ItemRepairBuffer only.
///
/// All other recipe configuration (requirements, outputs, craft duration,
/// station assignments, unit spawns, FakeItem outputs) is handled by the
/// existing RecipeEntryData fields and is independent of RecipeType.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecipeTypeEnum
{
    /// <summary>
    /// Standard crafting recipe — no repair cost.
    ///
    /// Component signature:
    ///   RecipeRequirementBuffer  — ingredients (may be empty)
    ///   RecipeOutputBuffer       — output items (real or FakeItem)
    ///   RecipeOutputUnitBuffer   — present, empty unless unit spawn data set
    ///   ItemRepairBuffer         — ABSENT
    ///
    /// Covers: consumables, refinement, gems, jewels, blood essence,
    /// weapon coatings, seeds, trader purchases, prisoner action recipes,
    /// unit spawn recipes, and soul shard extraction recipes.
    ///
    /// When the patcher converts TO Craft:
    ///   Removes ItemRepairBuffer if present.
    ///
    /// When the patcher converts FROM Craft to CraftRepairable:
    ///   Adds ItemRepairBuffer and populates it from RepairCosts config.
    /// </summary>
    Craft,

    /// <summary>
    /// Repairable equipment recipe — has repair costs.
    ///
    /// Component signature:
    ///   RecipeRequirementBuffer  — ingredients
    ///   RecipeOutputBuffer       — output item (typically weapon or armor)
    ///   RecipeOutputUnitBuffer   — present, empty
    ///   ItemRepairBuffer         — PRESENT, populated with repair cost entries
    ///
    /// Covers: weapons, armor, and any other craftable equipment
    /// that can be repaired at a workstation.
    ///
    /// When the patcher converts TO CraftRepairable:
    ///   Adds ItemRepairBuffer if absent, then populates from RepairCosts config.
    ///   RepairCosts must be provided — an empty repair cost list is valid
    ///   (buffer present but empty) but logs a warning since it produces a
    ///   recipe that appears repairable but has no repair cost defined.
    ///
    /// When the patcher converts FROM CraftRepairable to Craft:
    ///   Removes ItemRepairBuffer entirely.
    /// </summary>
    CraftRepairable,
}
