using System.Reflection;
using Stunlock.Core;
using Stunlock.Localization;
using LilithsMind.Prefabs;
using LilithsMind.Network;
using LilithsSoul.Foundation;

// ============================================================
//  LocalizationInjector — LilithsSoul
//  LilithsSoul/Services/LocalizationInjector.cs
//
//  Injects display name and tooltip overrides from a received
//  ServerSyncPayload into V Rising's live localization table:
//      Stunlock.Localization.Localization._LocalizedStrings
//
//  [CHANGED] Now reads from payload.ItemAppearanceOverrides instead
//            of separate DisplayNameOverrides / DescriptionTextOverrides.
//            Each LilithItemData entry carries both DisplayName
//            and DescriptionText — one iteration over the dict handles both.
//            Icon is passed to IconPatcher separately — this service
//            only handles text injection into _LocalizedStrings.
//
//  Lookup strategy (unchanged):
//  ─────────────────────────────
//  BuildLookupTable() scans LilithsMind definition classes once
//  at world ready and builds two dictionaries:
//    _nameToNameGuid — prefab Name/Prefab string → NameKey AssetGuid
//    _nameToDescGuid — prefab Name/Prefab string → DescKey AssetGuid
//
//  Entries with null NameKey or DescKey are skipped — partial
//  definitions cannot inject what they don't have.
//
//  [PERFORMANCE] Reflection over LilithsMind assembly runs once
//                at world ready — O(n) over all definition fields.
//                No ECS iteration. No PrefabCollectionSystem needed.
//                Injection is O(1) per entry — direct dict write.
//                ClearPrevious calls LoadDefaultLanguage() once per
//                server switch — reads from disk, paid once only.
// ============================================================

namespace LilithsSoul.Services;

public static class LocalizationInjector
{
    private const string LOG_SOURCE      = "LilithsSoul.LocalizationInjector";
    private const string PrefabNamespace = "LilithsMind.Prefabs.Definitions";

    // Built once at world ready from LilithsMind definitions.
    // Keyed by both Name (admin-facing) and Prefab string (game asset name).
    static readonly Dictionary<string, AssetGuid> _nameToNameGuid = new();
    static readonly Dictionary<string, AssetGuid> _nameToDescGuid = new();

    // Track injected AssetGuids so we can cleanly remove them on server switch.
    static readonly HashSet<AssetGuid> _injectedGuids = new();

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Builds both lookup tables from LilithsMind definition classes.
    /// Called by SyncReceiver.NotifyWorldReady().
    /// Safe to call multiple times — clears and rebuilds each call.
    /// [PERFORMANCE] O(n) reflection over LilithsMind definitions, once at world ready.
    /// </summary>
    public static void BuildLookupTable()
    {
        _nameToNameGuid.Clear();
        _nameToDescGuid.Clear();

        var mindAssembly    = typeof(PrefabDef).Assembly;
        var definitionTypes = mindAssembly
            .GetTypes()
            .Where(t =>
                t.IsClass &&
                t.IsAbstract &&
                t.IsSealed &&
                t.Namespace == PrefabNamespace)
            .ToList();

        if (definitionTypes.Count == 0)
        {
            SoulLogger.Warning(LOG_SOURCE,
                "No definition classes found in LilithsMind — " +
                "localization lookup will be empty.");
            return;
        }

        int nameCount = 0;
        int descCount = 0;

        foreach (var type in definitionTypes)
        {
            var fields = type
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(PrefabDef));

            foreach (var field in fields)
            {
                var def = (PrefabDef)field.GetValue(null)!;

                if (def.NameKey is not null)
                {
                    var nameGuid = ParseAssetGuid(def.NameKey);
                    if (!string.IsNullOrEmpty(def.Prefab))
                        _nameToNameGuid[def.Prefab] = nameGuid;
                    if (!string.IsNullOrEmpty(def.Name))
                        _nameToNameGuid[def.Name] = nameGuid;
                    nameCount++;
                }

                if (def.DescKey is not null)
                {
                    var descGuid = ParseAssetGuid(def.DescKey);
                    if (!string.IsNullOrEmpty(def.Prefab))
                        _nameToDescGuid[def.Prefab] = descGuid;
                    if (!string.IsNullOrEmpty(def.Name))
                        _nameToDescGuid[def.Name] = descGuid;
                    descCount++;
                }
            }
        }

        SoulLogger.Info(LOG_SOURCE,
            $"Lookup tables built — {nameCount} name key(s), {descCount} desc key(s) " +
            $"from {definitionTypes.Count} definition class(es).");
    }

    /// <summary>
    /// Injects display name and tooltip overrides from the payload into
    /// Localization._LocalizedStrings. Safe to call multiple times —
    /// previous overrides are cleared via LoadDefaultLanguage() first.
    ///
    /// [CHANGED] Reads from payload.ItemAppearanceOverrides instead of
    ///           separate DisplayNameOverrides / DescriptionTextOverrides dicts.
    ///           Icon field is intentionally ignored here — handled by
    ///           IconPatcher separately.
    /// </summary>
    public static void Inject(ServerSyncPayload payload)
    {
        ClearPrevious();

        var table = Localization._LocalizedStrings;
        if (table == null)
        {
            SoulLogger.Warning(LOG_SOURCE,
                "Localization._LocalizedStrings is null — not initialized yet.");
            return;
        }

        int nameCount    = 0;
        int tooltipCount = 0;
        int skipped      = 0;

        foreach (var (prefabName, appearance) in payload.ItemAppearanceOverrides)
        {
            // ── Display name ──────────────────────────────────
            if (appearance.DisplayName is not null)
            {
                if (_nameToNameGuid.TryGetValue(prefabName, out var nameGuid))
                {
                    table[nameGuid] = appearance.DisplayName;
                    _injectedGuids.Add(nameGuid);
                    nameCount++;
                }
                else
                {
                    SoulLogger.Warning(LOG_SOURCE,
                        $"No NameKey for '{prefabName}' — " +
                        "add NameKey to its PrefabDef in LilithsMind.");
                    skipped++;
                }
            }

            // ── DescriptionText ───────────────────────────────────────
            if (appearance.DescriptionText is not null)
            {
                if (_nameToDescGuid.TryGetValue(prefabName, out var descGuid))
                {
                    table[descGuid] = appearance.DescriptionText;
                    _injectedGuids.Add(descGuid);
                    tooltipCount++;
                }
                else
                {
                    SoulLogger.Warning(LOG_SOURCE,
                        $"No DescKey for '{prefabName}' — " +
                        "add DescKey to its PrefabDef in LilithsMind.");
                    skipped++;
                }
            }
        }

        SoulLogger.Info(LOG_SOURCE,
            $"Injected {nameCount} display name(s), {tooltipCount} tooltip(s) " +
            $"from server '{payload.ServerIdentity}'. " +
            $"{skipped} skipped (missing LilithsMind NameKey/DescKey).");
    }

    // ── Internal ─────────────────────────────────────────────

    static void ClearPrevious()
    {
        if (_injectedGuids.Count == 0) return;

        Localization.LoadDefaultLanguage();
        _injectedGuids.Clear();

        SoulLogger.Debug(LOG_SOURCE,
            "Previous localization overrides cleared via reload.");
    }

    static AssetGuid ParseAssetGuid(string guidString)
        => AssetGuid.FromString(guidString);
}