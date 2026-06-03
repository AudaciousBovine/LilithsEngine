// ============================================================
//  LilithItemConfig — LilithsHeart
//  LilithsHeart/Config/LilithItemConfig.cs
//
//  Pure data surface for all server-defined item overrides.
//  Holds the merged results of all Items/*.json files loaded
//  by the item config loader.
//
//  Two dictionaries — one per class — keyed by prefab name:
//    _appearance   → LilithItemData           (appearance fields)
//    _functional   → LilithItemFunctionalData  (functional fields)
//
//  Both are populated from the same JSON files in one pass.
//  Each service reads from the relevant dictionary only:
//    LocalizationService   → AppearanceOverrides (DisplayName, DescriptionText)
//    InterfaceService      → AppearanceOverrides (Icon)
//    ItemFunctionalService → FunctionalOverrides (StackSize)
//
//  [CHANGED] Replaces ItemAppearanceConfig. Split into two typed
//            dictionaries to cleanly separate appearance and
//            functional concerns while keeping one config file
//            per item on disk.
//
//  [PERFORMANCE] Two flat dictionaries — O(1) lookup per key.
//                Populated once at world ready by the item loader.
//                No file I/O occurs here.
// ============================================================

using LilithsMind.Data;

namespace LilithsHeart.Config;

public static class LilithItemConfig
{
    static readonly Dictionary<string, LilithItemData>           _appearance  = new();
    static readonly Dictionary<string, LilithItemFunctionalData> _functional  = new();

    /// <summary>
    /// All item appearance overrides keyed by prefab name.
    /// Read by LocalizationService (DisplayName, DescriptionText)
    /// and InterfaceService (Icon).
    /// </summary>
    public static IReadOnlyDictionary<string, LilithItemData> AppearanceOverrides => _appearance;

    /// <summary>
    /// All item functional overrides keyed by prefab name.
    /// Read by ItemFunctionalService (StackSize).
    /// </summary>
    public static IReadOnlyDictionary<string, LilithItemFunctionalData> FunctionalOverrides => _functional;

    /// <summary>
    /// True once the item config loader has completed its initial load.
    /// </summary>
    public static bool IsLoaded { get; private set; }

    /// <summary>Returns the appearance override for a prefab, or null if none exists.</summary>
    public static LilithItemData? GetAppearance(string prefabName)
        => _appearance.TryGetValue(prefabName, out var v) ? v : null;

    /// <summary>Returns the functional override for a prefab, or null if none exists.</summary>
    public static LilithItemFunctionalData? GetFunctional(string prefabName)
        => _functional.TryGetValue(prefabName, out var v) ? v : null;

    // ── Called by item config loader only ────────────────────

    internal static void Clear()
    {
        _appearance.Clear();
        _functional.Clear();
        IsLoaded = false;
    }

    /// <summary>
    /// Merges an appearance override entry.
    /// Later file wins per field — null fields do not overwrite existing values.
    /// </summary>
    internal static void AddAppearanceOverride(string key, LilithItemData incoming)
    {
        if (!_appearance.TryGetValue(key, out var existing))
        {
            _appearance[key] = incoming;
            return;
        }

        // Per-field merge — later file wins, nulls don't overwrite.
        if (incoming.DisplayName     is not null) existing.DisplayName     = incoming.DisplayName;
        if (incoming.DescriptionText is not null) existing.DescriptionText = incoming.DescriptionText;
        if (incoming.Icon            is not null) existing.Icon            = incoming.Icon;
    }

    /// <summary>
    /// Merges a functional override entry.
    /// Later file wins per field — null fields do not overwrite existing values.
    /// </summary>
    internal static void AddFunctionalOverride(string key, LilithItemFunctionalData incoming)
    {
        if (!_functional.TryGetValue(key, out var existing))
        {
            _functional[key] = incoming;
            return;
        }

        // Per-field merge — later file wins, nulls don't overwrite.
        if (incoming.StackSize.HasValue) existing.StackSize = incoming.StackSize;
    }

    internal static void MarkLoaded() => IsLoaded = true;
}