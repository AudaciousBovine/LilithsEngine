// ============================================================
//  LilithItemData — LilithsMind
//  LilithsMind/Data/LilithItemData.cs
//
//  Unified item override DTO. All fields are optional — admins
//  only populate what they want to change, Soul and Heart
//  silently skip null fields.
//
//  This file contains two classes with a clear separation of
//  responsibility:
//
//  LilithItemData         — appearance fields, synced to Soul
//  LilithItemFunctionalData — functional fields, server-only
//
//  Both are deserialized from the same Items/*.json files by
//  LilithItemConfig's loader. Each service reads only its own
//  class:
//    LocalizationService  → LilithItemData.DisplayName/DescriptionText
//    InterfaceService     → LilithItemData.Icon
//    ItemFunctionalService → LilithItemFunctionalData.StackSize
//
//  [CHANGED] Renamed from ItemAppearanceData → LilithItemData.
//            Follows the Lilith* naming pattern for shared DTOs
//            (LilithRecipeData, LilithStationData, etc.).
//            Functional fields split into LilithItemFunctionalData
//            to keep appearance and functional concerns separate.
//
//  [CHANGED] StackSize added to LilithItemFunctionalData.
//            Server-side only — patches ItemData.MaxAmount on
//            prefab entities at world ready via ItemFunctionalService.
//            Never included in ServerSyncPayload.
//
//  [PERFORMANCE] Plain DTOs — no Unity or game dependencies.
//                LilithItemData serialized as part of
//                ServerSyncPayload by Heart, deserialized by Soul.
//                LilithItemFunctionalData never crosses the wire.
// ============================================================

namespace LilithsMind.Data;

/// <summary>
/// Appearance override fields for a single item.
/// Synced from Heart to Soul via ServerSyncPayload.ItemAppearanceOverrides.
///
/// Owned by:
///   LocalizationService — DisplayName, DescriptionText
///   InterfaceService    — Icon
/// </summary>
public sealed class LilithItemData
{
    /// <summary>
    /// Custom display name for this item.
    /// Applied client-side by LocalizationService / LocalizationPatcher.
    /// Injected into Localization._LocalizedStrings via a minted AssetGuid.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Custom tooltip body text for this item.
    /// Applied client-side by LocalizationService / DescriptionPatcher.
    /// Injected into Localization._LocalizedStrings via a minted AssetGuid.
    /// </summary>
    public string? DescriptionText { get; set; }

    /// <summary>
    /// Icon override for this item.
    /// Applied client-side by InterfaceService / IconPatcher.
    /// Resolution order:
    ///   1. Filename without extension → local PNG in Icons/ folder
    ///   2. Sprite name → in-game sprite from Resources
    ///   3. https:// URL → downloaded and cached to Icons/ folder
    /// </summary>
    public string? Icon { get; set; }
}

/// <summary>
/// Functional override fields for a single item.
/// Server-side only — never synced to Soul, never included in
/// ServerSyncPayload. Applied at world ready by ItemFunctionalService.
///
/// Owned by:
///   ItemFunctionalService — StackSize (patches ItemData.MaxAmount)
/// </summary>
public sealed class LilithItemFunctionalData
{
    /// <summary>
    /// Maximum stack size for this item.
    /// Patches ProjectM.ItemData.MaxAmount on the item's prefab entity.
    /// Null = keep vanilla stack size.
    ///
    /// [PERFORMANCE] Applied once at world ready — no per-frame cost.
    /// </summary>
    public int? StackSize { get; set; }
}