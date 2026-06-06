using LilithsMind.Data;

namespace LilithsHeart.Config;

public static class LilithItemConfig
{
    static readonly Dictionary<string, LilithItemData> _overrides = new();

    /// <summary>
    /// All item overrides keyed by prefab name.
    /// Populated by ItemService.Initialize() / Reload().
    /// Each service reads the fields it owns from each entry.
    /// </summary>
    public static IReadOnlyDictionary<string, LilithItemData> Overrides => _overrides;

    /// <summary>
    /// True once ItemService has completed its initial load.
    /// </summary>
    public static bool IsLoaded { get; private set; }

    /// <summary>
    /// Returns the override for a prefab, or null if none exists.
    /// </summary>
    public static LilithItemData? GetOverride(string prefabName)
        => _overrides.TryGetValue(prefabName, out var v) ? v : null;

    // ── Called by ItemService only ────────────────────────────

    internal static void Clear()
    {
        _overrides.Clear();
        IsLoaded = false;
    }

    /// <summary>
    /// Adds or merges an item override entry.
    /// Later file wins per field — null fields do not overwrite
    /// existing values. All fields on LilithItemData follow the
    /// same merge rule regardless of which service owns them.
    /// </summary>
    internal static void AddOverride(string key, LilithItemData incoming)
    {
        if (!_overrides.TryGetValue(key, out var existing))
        {
            _overrides[key] = incoming;
            return;
        }

        // Per-field merge — later file wins, nulls don't overwrite.
        if (incoming.DisplayName     is not null) existing.DisplayName     = incoming.DisplayName;
        if (incoming.DescriptionText is not null) existing.DescriptionText = incoming.DescriptionText;
        if (incoming.Icon            is not null) existing.Icon            = incoming.Icon;
        if (incoming.StackSize.HasValue)          existing.StackSize       = incoming.StackSize;
    }

    internal static void MarkLoaded() => IsLoaded = true;
}