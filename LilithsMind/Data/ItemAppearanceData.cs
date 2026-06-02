// ============================================================
//  ItemAppearanceData — LilithsMind
//  LilithsMind/Data/ItemAppearanceData.cs
//
//  DTO representing all appearance overrides for a single item.
//  Used as the value type in ServerSyncPayload.ItemAppearanceOverrides.
//
//  All fields are optional — an admin only needs to populate the
//  fields they want to change. Soul silently skips null fields.
//
//  Icon resolution order (Soul-side IconPatcher):
//    1. Local PNG filename — recursive scan of Icons/ folder,
//       matched by filename without extension (e.g. "vitae")
//    2. In-game sprite name — Resources.FindObjectsOfTypeAll<Sprite>()
//       matched by sprite.name (e.g. "Icon_BloodOrb")
//    3. HTTPS URL — downloaded to Icons/ cache folder on first
//       encounter, resolved from cache on subsequent connects
//
//  [PERFORMANCE] Plain DTO — no Unity or game dependencies.
//                Serialized as part of ServerSyncPayload by Heart,
//                deserialized by Soul. Zero allocation at read time.
// ============================================================

namespace LilithsMind.Data;

public sealed class ItemAppearanceData
{
    /// <summary>
    /// Custom display name for this item.
    /// Injected into Localization._LocalizedStrings via the item's NameKey.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Custom tooltip text for this item.
    /// Injected into Localization._LocalizedStrings via the item's DescKey.
    /// </summary>
    public string? DescriptionText { get; set; }

    /// <summary>
    /// Icon override for this item. Resolved in order:
    ///   1. Filename without extension → local PNG in Icons/ folder
    ///   2. Sprite name → in-game sprite from Resources
    ///   3. https:// URL → downloaded and cached to Icons/ folder
    /// </summary>
    public string? Icon { get; set; }
}