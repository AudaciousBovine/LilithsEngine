// ============================================================
//  LilithRecipeData — LilithsMind
//  LilithsMind/Network/LilithRecipeData.cs
//
//  DTO describing a single recipe's full configuration as sent
//  inside ServerSyncPayload.RecipeOverrides.
//
//  [CHANGED] Migrated and simplified from duplicate definitions in:
//              LilithsHeart/Network/LilithRecipeData.cs
//              LilithsSoul/Network/LilithRecipeData.cs
//            Both files are now deleted. This is the single
//            definition shared between Heart and Soul.
//
//  [CHANGED] LilithRecipeData (formerly a separate nested type) has
//            been eliminated. Requirements and Outputs are now
//            Dictionary<string, int> — prefab name → amount.
//            This removes an unnecessary type, flattens the
//            structure, and makes recipe config more readable.
//
//  Only fields the client HUD actually reads are included:
//    • CraftDuration  — displayed in the craft button
//    • Requirements   — ingredient list shown before crafting
//    • Outputs        — output item list shown in the recipe card
//
//  Soul uses these to patch RecipeData, CookbookItemData Buffer,
//  and CookbookItemData Buffer on client-side prefab entities so the
//  UI reflects what Heart enforces server-side.
//
//  Usage example:
//  ──────────────
//  new LilithRecipeData
//  {
//      CraftDuration = 30f,
//      Requirements  = new() { ["Item_Ingredient_Mineral_CopperIngot"] = 3 },
//      Outputs       = new() { ["Item_Weapon_Sword_T01"] = 1 }
//  }
//
//  [PERFORMANCE] Plain DTO — no ECS types, no Unity dependencies.
//                Deserialized once on connect as part of the
//                larger ServerSyncPayload.
// ============================================================

namespace LilithsMind.Data;

/// <summary>
/// Full configuration data for a single recipe.
/// Keyed by recipe prefab name in ServerSyncPayload.RecipeOverrides.
/// </summary>
public sealed class LilithRecipeData
{
    /// <summary>
    /// Craft duration in seconds.
    /// Patched into RecipeData.CraftDuration on the client prefab entity.
    /// Displayed in the craft button.
    /// </summary>
    public float CraftDuration { get; set; }

    /// <summary>
    /// Ingredient requirements — prefab name → stack amount.
    /// e.g. { "Item_Ingredient_Mineral_CopperIngot": 3 }
    /// Patched into CookbookItemData Buffer on the client prefab entity.
    /// </summary>
    public Dictionary<string, int> Requirements { get; set; } = new();

    /// <summary>
    /// Output items — prefab name → stack amount.
    /// e.g. { "Item_Weapon_Sword_T01": 1 }
    /// Patched into CookbookItemData Buffer on the client prefab entity.
    /// </summary>
    public Dictionary<string, int> Outputs { get; set; } = new();
}