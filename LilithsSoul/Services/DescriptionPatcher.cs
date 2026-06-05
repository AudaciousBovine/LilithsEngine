using System.Reflection;
using ProjectM;               // ManagedItemData, ManagedDataSystem
using ProjectM.UI;            // LocalizedStringBuilderBase
using Stunlock.Core;          // PrefabGUID
using Stunlock.Localization;  // LocalizationKey, AssetGuid, Localization
using LilithsMind.Prefabs;    // PrefabDef
using LilithsMind.Network;
using LilithsSoul.Foundation; // SoulLogger, Soul

// ============================================================
//  DescriptionPatcher — LilithsSoul
//  LilithsSoul/Services/DescriptionPatcher.cs
//
//  Sole owner of item DESCRIPTION (tooltip body) overrides on the
//  client. Data-layer repoint — mirrors LocalizationPatcher (names).
//
//  THE KEY INSIGHT (after a long UI-patch detour):
//  ─────────────────────────────────────────────────
//  ManagedItemData.Description is a LocalizedStringBuilderBase, which the
//  dnSpy dump revealed is a STRUCT (value type) whose FIRST field is:
//      [FieldOffset(0)] public LocalizationKey Key;
//  i.e. the description body resolves from a LocalizationKey — the SAME
//  type as ManagedItemData.Name, which we already repoint successfully.
//  The builder's Build(EntityManager, Entity) resolves Key → text.
//
//  Why earlier "Description doesn't persist" was a false negative:
//  Description's getter returns a COPY of the struct (value semantics).
//  Mutating that copy's Key and discarding it changes nothing. The fix is
//  to mutate the copy's Key AND ASSIGN THE WHOLE STRUCT BACK through the
//  public setter — exactly how assigning Name (also a value type) persists.
//  That write-back step is what was missing; this is the name recipe, one
//  level in.
//
//  Repoint recipe (identical in spirit to LocalizationPatcher):
//    1. Mint a fresh AssetGuid (unique by construction).
//    2. Localization._LocalizedStrings[guid] = ourText.
//    3. var d = item.Description;            // struct copy
//       d.Key = new LocalizationKey(guid);   // point at our minted string
//       item.Description = d;                // WRITE THE STRUCT BACK
//  No UI patch, no Harmony on the tooltip pipeline (which crashes/never
//  fires in this build) — pure data-layer, the approach proven by names+icons.
//
//  Lifecycle — mirrors LocalizationPatcher / IconPatcher:
//    SyncReceiver calls Clear() then Build() each payload.
//    Clear restores each item's captured original Description struct;
//    Build re-captures (now-vanilla) originals, mints, and repoints.
//
//  [CHANGED] RepointDescription() now passes description text through
//            ColorTranslator.Translate() before injecting into
//            _LocalizedStrings. V Rising named colour tags
//            (e.g. <teal1>, </c>) are converted to Unity rich text
//            (<color=#...>, </color>) at inject time so they render
//            correctly — injected strings bypass V Rising's tag
//            processing layer but Unity's render layer handles
//            rich text natively.
//
//  [PERFORMANCE] Per description: one dict write + one struct write-back —
//                O(1), one-time at apply. Steady state ZERO (no getter patch,
//                no per-frame work; the tooltip resolves our key natively).
//                Each Build mints fresh keys, leaving prior strings as
//                harmless orphans in _LocalizedStrings (a few per apply);
//                cleanup deferred, same as LocalizationPatcher.
// ============================================================

namespace LilithsSoul.Services;

public static class DescriptionPatcher
{
    private const string LOG_SOURCE      = "LilithsSoul.DescriptionPatcher";
    private const string PrefabNamespace = "LilithsMind.Prefabs.Definitions";

    // prefab Name/Prefab string → PrefabGUID. Built once at world ready.
    static readonly Dictionary<string, PrefabGUID> _descToPrefabGuid
        = new(StringComparer.OrdinalIgnoreCase);

    // Captured original Description structs for restore, by prefab GuidHash.
    static readonly Dictionary<int, LocalizedStringBuilderBase> _previousDescriptions = new();

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Builds the prefab-name → PrefabGUID map from LilithsMind definitions.
    /// Called by SyncReceiver.NotifyWorldReady().
    /// [PERFORMANCE] O(n) reflection over LilithsMind definitions, once.
    /// </summary>
    public static void BuildMap()
    {
        _descToPrefabGuid.Clear();

        var mindAssembly    = typeof(PrefabDef).Assembly;
        var definitionTypes = mindAssembly.GetTypes()
            .Where(t => t.IsClass && t.IsAbstract && t.IsSealed && t.Namespace == PrefabNamespace)
            .ToList();

        int count = 0;
        foreach (var type in definitionTypes)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static)
                         .Where(f => f.FieldType == typeof(PrefabDef)))
            {
                var def  = (PrefabDef)field.GetValue(null)!;
                var guid = new PrefabGUID(def.GuidHash);

                if (def.Prefab is not null)
                    _descToPrefabGuid[def.Prefab] = guid;
                else
                    SoulLogger.Warning(LOG_SOURCE,
                        $"Definition {type.Name}.{field.Name} has null Prefab — skipped. Fix the entry.");

                if (def.Name is not null)
                    _descToPrefabGuid[def.Name] = guid;
                count++;
            }
        }

        SoulLogger.Info(LOG_SOURCE,
            $"Name→PrefabGUID map built — {count} definition(s) from {definitionTypes.Count} class(es).");
    }

    /// <summary>
    /// Repoints item descriptions from payload.ItemAppearanceOverrides.
    /// Call AFTER Clear() on each payload (mirrors LocalizationPatcher).
    /// DisplayName / Icon fields are ignored here.
    /// </summary>
    public static void Build(ServerSyncPayload payload)
    {
        if (Soul.ClientWorld == null)
        {
            SoulLogger.Warning(LOG_SOURCE, "Client world not ready — cannot apply descriptions.");
            return;
        }

        var registry = Soul.ClientWorld
            .GetExistingSystemManaged<ManagedDataSystem>()
            ?.ManagedDataRegistry;
        if (registry == null)
        {
            SoulLogger.Warning(LOG_SOURCE, "ManagedDataRegistry not available — cannot apply descriptions.");
            return;
        }

        var table = Localization._LocalizedStrings;
        if (table == null)
        {
            SoulLogger.Warning(LOG_SOURCE, "_LocalizedStrings null — cannot apply descriptions.");
            return;
        }

        int applied = 0;
        int skipped = 0;

        foreach (var (prefabName, appearance) in payload.ItemAppearanceOverrides)
        {
            if (appearance.DescriptionText is null) continue;

            if (!_descToPrefabGuid.TryGetValue(prefabName, out var guid))
            {
                SoulLogger.Warning(LOG_SOURCE,
                    $"No PrefabGUID for '{prefabName}' — add a stub PrefabDef in LilithsMind.");
                skipped++;
                continue;
            }

            var item = registry.GetOrDefault<ManagedItemData>(guid);
            if (item == null)
            {
                SoulLogger.Warning(LOG_SOURCE, $"No ManagedItemData for '{prefabName}' — skipping description.");
                skipped++;
                continue;
            }

            try
            {
                RepointDescription(item, guid.GuidHash, appearance.DescriptionText, table);
                applied++;
            }
            catch (Exception ex)
            {
                SoulLogger.Warning(LOG_SOURCE, $"Description repoint failed for '{prefabName}': {ex.Message}");
                skipped++;
            }
        }

        SoulLogger.Info(LOG_SOURCE,
            $"Descriptions repointed — {applied} applied, {skipped} skipped.");
    }

    /// <summary>
    /// Restores every repointed item's original Description. Called before
    /// each Build (mirrors LocalizationPatcher.ClearPrevious()).
    /// </summary>
    public static void Clear()
    {
        if (_previousDescriptions.Count == 0) return;
        if (Soul.ClientWorld == null) return;

        var registry = Soul.ClientWorld
            .GetExistingSystemManaged<ManagedDataSystem>()
            ?.ManagedDataRegistry;
        if (registry == null) return;

        int restored = 0;
        foreach (var (guidHash, originalDesc) in _previousDescriptions)
        {
            var item = registry.GetOrDefault<ManagedItemData>(new PrefabGUID(guidHash));
            if (item == null) continue;

            try
            {
                item.Description = originalDesc;   // write the original struct back
                restored++;
            }
            catch (Exception ex)
            {
                SoulLogger.Warning(LOG_SOURCE, $"Failed to restore description for GUID {guidHash}: {ex.Message}");
            }
        }

        SoulLogger.Debug(LOG_SOURCE, $"Restored {restored} original description(s).");
        _previousDescriptions.Clear();
    }

    // ── Internal ─────────────────────────────────────────────

    static void RepointDescription(
        ManagedItemData item,
        int guidHash,
        string newText,
        Il2CppSystem.Collections.Generic.Dictionary<AssetGuid, string> table)
    {
        // Capture the vanilla Description struct (first wins) for restore.
        _previousDescriptions.TryAdd(guidHash, item.Description);

        var g = Mint();                 // fresh AssetGuid, unique by construction

        // [CHANGED] Translate V Rising named colour tags (e.g. <teal1>, </c>)
        // to Unity rich text (<color=#...>, </color>) before injecting.
        // Injected strings bypass V Rising's tag processing layer but Unity's
        // render layer handles rich text natively.
        table[g] = ColorTranslator.Translate(newText); // inject translated string

        // CRITICAL: get the struct copy, set its Key, WRITE THE WHOLE STRUCT
        // BACK. Mutating the copy alone would be discarded (value semantics) —
        // the write-back is what persists, exactly like assigning Name.
        var d = item.Description;
        d.Key = MakeKey(g);
        item.Description = d;
    }

    static AssetGuid Mint() => AssetGuid.FromString(Guid.NewGuid().ToString());

    static LocalizationKey MakeKey(AssetGuid guid) => new LocalizationKey(guid);
}