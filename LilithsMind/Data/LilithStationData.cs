// ============================================================
//  LilithStationData — LilithsMind
//  LilithsMind/Network/LilithStationData.cs
//
//  DTO describing which recipes to add or remove from a specific
//  crafting station's WorkstationRecipesBuffer on the client.
//
//  [CHANGED] Migrated from duplicate definitions in:
//              LilithsHeart/Network/StationLilithRecipeData.cs
//              LilithsSoul/Network/StationLilithRecipeData.cs
//            Both files are now deleted. This is the single
//            definition shared between Heart and Soul.
//
//  [CHANGED] Renamed from LilithsStationData → LilithStationData.
//            Removes the erroneous 's' to match the suite naming
//            convention established for Lilith-prefixed types.
//
//  Keyed by station prefab name in
//  ServerSyncPayload.StationRecipeOverrides.
//
//  [PERFORMANCE] Small flat lists — no nested collections.
//                Serialized once per connect.
// ============================================================

namespace LilithsMind.Data;

/// <summary>
/// Describes the recipe list configuration for a single crafting station.
/// Keyed by station prefab name in ServerSyncPayload.StationRecipeOverrides.
/// </summary>
public sealed class LilithStationData
{
    /// <summary>
    /// Recipe prefab names to add to the station's WorkstationRecipesBuffer.
    /// </summary>
    public List<string> RecipesToAdd { get; set; } = new();

    /// <summary>
    /// Recipe prefab names to remove from the station's WorkstationRecipesBuffer.
    /// </summary>
    public List<string> RecipesToRemove { get; set; } = new();
}