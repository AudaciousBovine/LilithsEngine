using LilithsMind.Data;

// ============================================================
//  ItemAppearanceConfig — LilithsHeart
//  LilithsHeart/Config/ItemAppearanceConfig.cs
//
//  Pure data surface for all server-defined item appearance
//  overrides. Holds the merged results of all JSON files loaded
//  by LocalizationService across all registered directories.
//
//  [CHANGED] Replaced two flat dictionaries (_displayNames,
//            _tooltips) with a single dictionary keyed by prefab
//            name and valued by ItemAppearanceData. This matches
//            the new combined Items/ JSON format and the updated
//            ServerSyncPayload.ItemAppearanceOverrides field.
//
//            Old accessors GetDisplayName() and GetDescriptionText() are
//            removed. Consumers read from Overrides directly or
//            call GetOverride() for a single entry.
//
//  Key format: prefab Name (from LilithsMind PrefabDef.Name) or
//  Prefab string (e.g. "Item_BloodEssence_T01"). Soul resolves
//  these to AssetGuids via LilithsMind's NameKey/DescKey fields.
//
//  ⚠️  IMPORTANT: DisplayName/DescriptionText overrides only take effect
//  on the client if LilithsMind has a PrefabDef entry with NameKey
//  (for DisplayName) or DescKey (for DescriptionText) populated for that
//  prefab. Icon overrides require the icon source to be resolvable
//  by Soul's IconPatcher. Unresolvable entries are skipped with a
//  warning in the Soul log.
//
//  [PERFORMANCE] Single flat dictionary — O(1) lookup per key.
//                Populated once at world ready by LocalizationService.
//                No file I/O occurs here.
// ============================================================

namespace LilithsHeart.Config;

public static class ItemAppearanceConfig
{
    static readonly Dictionary<string, ItemAppearanceData> _overrides = new();

    /// <summary>
    /// All item appearance overrides keyed by prefab name.
    /// Populated by LocalizationService.Initialize() / Reload().
    /// </summary>
    public static IReadOnlyDictionary<string, ItemAppearanceData> Overrides => _overrides;

    /// <summary>
    /// True once LocalizationService has completed its initial load.
    /// </summary>
    public static bool IsLoaded { get; private set; }

    /// <summary>
    /// Returns the appearance override for a prefab, or null if none exists.
    /// </summary>
    public static ItemAppearanceData? GetOverride(string prefabName)
        => _overrides.TryGetValue(prefabName, out var v) ? v : null;

    // ── Called by LocalizationService only ───────────────────

    /// <summary>
    /// Clears the overrides dictionary and resets IsLoaded.
    /// Called by LocalizationService before reloading.
    /// </summary>
    internal static void Clear()
    {
        _overrides.Clear();
        IsLoaded = false;
    }

    /// <summary>
    /// Adds or merges an appearance override entry.
    /// If an entry for the key already exists, only non-null fields
    /// from the incoming data overwrite the existing entry — this
    /// allows multiple files to contribute different fields for the
    /// same item (e.g. one file sets DisplayName, another sets Icon).
    /// Called by LocalizationService during file loading.
    /// </summary>
    internal static void AddOverride(string key, ItemAppearanceData incoming)
    {
        if (!_overrides.TryGetValue(key, out var existing))
        {
            _overrides[key] = incoming;
            return;
        }

        // Merge — later file wins per field, not per entry.
        if (incoming.DisplayName is not null) existing.DisplayName = incoming.DisplayName;
        if (incoming.DescriptionText     is not null) existing.DescriptionText     = incoming.DescriptionText;
        if (incoming.Icon        is not null) existing.Icon        = incoming.Icon;
    }

    /// <summary>
    /// Marks the config as fully loaded.
    /// Called by LocalizationService after all files are processed.
    /// </summary>
    internal static void MarkLoaded() => IsLoaded = true;
}