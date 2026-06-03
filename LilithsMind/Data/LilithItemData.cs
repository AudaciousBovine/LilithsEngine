// ============================================================
//  LilithItemData — LilithsMind
//  LilithsMind/Data/LilithItemData.cs
//
//  Unified item override DTO. All fields are optional — null
//  means keep vanilla. Admins only populate what they want to change.
//
//  Field ownership:
//    DisplayName     → LocalizationService / LocalizationPatcher (Soul)
//    DescriptionText → LocalizationService / DescriptionPatcher (Soul)
//    Icon            → InterfaceService / IconPatcher (Soul)
//    ChangesEnabled  → gates all functional fields
//    StackSize       → ItemFunctionService (Cookbook, server-only)
//
//  Appearance fields always apply when non-null — no gate needed.
//  Functional fields only apply when ChangesEnabled = true.
//
//  [CHANGED] ChangesEnabled added — gates StackSize and any future
//            functional fields. Appearance fields are ungated.
//  [CHANGED] Field order: DisplayName, DescriptionText, Icon,
//            ChangesEnabled, StackSize. Appearance first, then
//            the functional gate, then functional values.
//
//  [PERFORMANCE] Plain DTO — no Unity or game dependencies.
//                Appearance fields serialized in ServerSyncPayload.
//                ChangesEnabled and StackSize never cross the wire.
// ============================================================

namespace LilithsMind.Data;

public sealed class LilithItemData
{
    /// <summary>
    /// Custom display name for this item.
    /// Applied client-side by LocalizationPatcher.
    /// Always applied when non-null — no ChangesEnabled gate.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Custom tooltip body text for this item.
    /// Applied client-side by DescriptionPatcher.
    /// Always applied when non-null — no ChangesEnabled gate.
    /// </summary>
    public string? DescriptionText { get; set; }

    /// <summary>
    /// Icon override for this item.
    /// Applied client-side by IconPatcher.
    /// Resolution order:
    ///   1. Filename without extension → local PNG in Icons/ folder
    ///   2. Sprite name → in-game sprite from Resources
    ///   3. https:// URL → downloaded and cached to Icons/ folder
    /// Always applied when non-null — no ChangesEnabled gate.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// When false, all functional fields (StackSize and any future
    /// additions) are ignored. Appearance fields are unaffected.
    /// Set to true to activate functional changes for this item.
    /// </summary>
    public bool ChangesEnabled { get; set; } = false;

    /// <summary>
    /// Maximum stack size for this item.
    /// Patches ProjectM.ItemData.MaxAmount on the item's prefab entity.
    /// Owned by LilithsCookbook's ItemFunctionService.
    /// Only applied when ChangesEnabled = true.
    /// Server-side only — never synced to Soul.
    /// Null = keep vanilla stack size.
    ///
    /// [PERFORMANCE] Applied once at world ready — no per-frame cost.
    /// </summary>
    public int? StackSize { get; set; }
}