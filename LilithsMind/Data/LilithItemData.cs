// ============================================================
//  LilithItemData — LilithsMind
//  LilithsMind/Data/LilithItemData.cs
//
//  Unified item override DTO. All fields are optional — admins
//  only populate what they want to change.
//
//  All item override fields live here — appearance and functional
//  alike. Each field is owned by a specific service that reads
//  only what it needs:
//
//    LocalizationService  (Heart)    — DisplayName, DescriptionText
//    InterfaceService     (Heart)    — Icon
//    ItemFunctionService  (Cookbook) — StackSize
//
//  Appearance fields (DisplayName, DescriptionText, Icon) travel
//  in ServerSyncPayload.ItemAppearanceOverrides to Soul.
//  StackSize is server-only — Heart's payload builder ignores it.
//
//  [CHANGED] LilithItemFunctionalData removed — StackSize folds
//            directly into this class. One DTO, all item fields,
//            each service reads what it owns.
//
//  [CHANGED] StackSize added. Owned by LilithsCookbook's
//            ItemFunctionService which patches ItemData.MaxAmount
//            on prefab entities at world ready. Never synced to Soul.
//
//  [PERFORMANCE] Plain DTO — no Unity or game dependencies.
//                Appearance fields serialized as part of
//                ServerSyncPayload. StackSize never crosses the wire.
// ============================================================

namespace LilithsMind.Data;

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

    /// <summary>
    /// Maximum stack size for this item.
    /// Patches ProjectM.ItemData.MaxAmount on the item's prefab entity.
    /// Owned by LilithsCookbook's ItemFunctionService.
    /// Server-side only — never synced to Soul.
    /// Null = keep vanilla stack size.
    ///
    /// [PERFORMANCE] Applied once at world ready — no per-frame cost.
    /// </summary>
    public int? StackSize { get; set; }
}