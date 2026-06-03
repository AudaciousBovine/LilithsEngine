// ============================================================
//  CookbookRecipeData — LilithsCookbook
//  LilithsCookbook/Data/CookbookRecipeData.cs
//
//  Top-level container and entry record for recipe config files.
//  Deserialized from *.json files in:
//      BepInEx/config/LilithsHeart/Recipes/
//
//  [CHANGED] RecipeEntryData gains an optional Stations list.
//            Station membership for a recipe is now declared
//            inline with the recipe itself — no separate
//            Stations/*.json files or CookbookStationData needed.
//
//            Stations: null  → do not modify station membership
//            Stations: []    → remove recipe from ALL stations
//            Stations: [...] → explicit set of stations that
//                              should contain this recipe;
//                              StationSystem diffs vs. vanilla.
//
//            CookbookStationData and StationEntryData are retired.
//            The Stations/ config directory is no longer created
//            or scanned. CookbookLoader.LoadStations() is removed.
//
//  [CHANGED] CookbookItemData, RecipeRepairCost, and RecipeUnitOutput
//            previously consolidated into CookbookItemData.
//            All list fields use CookbookItemData.
//
//  [PERFORMANCE] Plain DTOs — no ECS types, no Unity dependencies.
//                Deserialized once at startup by CookbookLoader.
// ============================================================

namespace LilithsCookbook.Data;

/// <summary>
/// Top-level container for all recipe config entries.
/// Deserialized from *.json files in the Recipes directory.
/// Multiple files are merged by CookbookLoader — later files win on key conflicts.
/// </summary>
public class CookbookRecipeData
{
    /// <summary>
    /// Recipe entries keyed by prefab name or LilithsMind Name alias.
    /// e.g. "Recipe_Weapon_Sword_T01_Bone" or "BoneSword"
    /// </summary>
    public Dictionary<string, RecipeEntryData> Recipes { get; set; } = new();
}

/// <summary>
/// Full configuration record for a single recipe.
/// All fields are nullable — only specified fields are applied.
/// Unspecified fields retain their vanilla values.
/// </summary>
public class RecipeEntryData
{
    /// <summary>
    /// When false, this entry is skipped entirely during apply.
    /// Set to true to activate changes for this recipe.
    /// </summary>
    public bool ChangesEnabled { get; set; } = false;

    /// <summary>Craft duration in seconds. Null = keep vanilla.</summary>
    public float? CraftDuration { get; set; }

    /// <summary>Whether the recipe is always unlocked. Null = keep vanilla.</summary>
    public bool? AlwaysUnlocked { get; set; }

    /// <summary>Whether the recipe is hidden in the station UI. Null = keep vanilla.</summary>
    public bool? HideInStation { get; set; }

    /// <summary>Whether the recipe ignores server settings. Null = keep vanilla.</summary>
    public bool? IgnoreServerSettings { get; set; }

    /// <summary>HUD sort order for this recipe. Null = keep vanilla.</summary>
    public int? HudSortingOrder { get; set; }

    /// <summary>
    /// Ingredient requirements. Null = keep vanilla requirements.
    /// Each entry is an item prefab name and stack amount.
    /// </summary>
    public List<CookbookItemData>? Requirements { get; set; }

    /// <summary>
    /// Output items. Null = keep vanilla outputs.
    /// Each entry is an item prefab name and stack amount.
    /// </summary>
    public List<CookbookItemData>? Outputs { get; set; }

    // ── Optional buffer control fields ───────────────────────────────────────
    // null  → not specified; buffer is left untouched
    // false → remove the buffer entirely from the entity
    // true  → ensure buffer exists and apply the list below

    /// <summary>Controls whether ItemRepairBuffer is present. Null = untouched.</summary>
    public bool? UseRepairCosts { get; set; }

    /// <summary>
    /// Repair cost items. Only applied when UseRepairCosts = true.
    /// Each entry is an item prefab name and stack amount.
    /// </summary>
    public List<CookbookItemData>? RepairCosts { get; set; }

    /// <summary>Controls whether RecipeOutputUnitBuffer is present. Null = untouched.</summary>
    public bool? UseUnitOutputs { get; set; }

    /// <summary>
    /// Unit outputs (dominated servants, etc). Only applied when UseUnitOutputs = true.
    /// Each entry is a unit prefab name and stack amount.
    /// </summary>
    public List<CookbookItemData>? UnitOutputs { get; set; }

    /// <summary>Controls whether RecipeLinkBuffer is present. Null = untouched.</summary>
    public bool? UseRecipeLinks { get; set; }

    /// <summary>
    /// Linked recipe prefab names. Only applied when UseRecipeLinks = true.
    /// </summary>
    public List<string>? RecipeLinks { get; set; }

    /// <summary>
    /// [CHANGED] Station membership for this recipe.
    ///
    /// Replaces the separate Stations/*.json config system.
    /// Each entry is a station prefab name or LilithsMind Name alias.
    /// e.g. "Blacksmith", "TM_Blacksmith_Stations_Standard"
    ///
    /// Null   → do not modify which stations carry this recipe.
    /// []     → remove this recipe from every station that currently has it.
    /// [...]  → StationSystem will add this recipe to each named station
    ///          and remove it from any station not listed that currently
    ///          carries it (diff against vanilla state).
    ///
    /// [PERFORMANCE] Resolved once at startup by StationSystem.ApplyChanges().
    ///               No per-frame cost.
    /// </summary>
    public List<string>? Stations { get; set; }
}