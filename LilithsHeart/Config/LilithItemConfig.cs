using LilithsMind.Data;

namespace LilithsHeart.Config;

public static class LilithItemConfig
{
    static readonly Dictionary<string, LilithItemData> _overrides = new();

    public static IReadOnlyDictionary<string, LilithItemData> Overrides => _overrides;

    public static bool IsLoaded { get; private set; }

    public static LilithItemData? GetOverride(string prefabName)
        => _overrides.TryGetValue(prefabName, out var v) ? v : null;

    // ── Called by ItemService only ────────────────────────────

    internal static void Clear()
    {
        _overrides.Clear();
        IsLoaded = false;
    }

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