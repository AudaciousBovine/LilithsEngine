using System.Reflection;
using ProjectM;
using Stunlock.Localization;  // LocalizationKey, AssetGuid, Localization
using Stunlock.Core;          // PrefabGUID
using LilithsMind.Prefabs;    // PrefabDef
using LilithsMind.Network;
using LilithsSoul.Foundation;

// ============================================================
//  LocalizationPatcher — LilithsSoul
//  LilithsSoul/Services/LocalizationPatcher.cs
//
//  Sole owner of item DISPLAY-NAME localization on the client.
//  Replaces LocalizationInjector (retired) for the name path.
//
//  Why this exists (the "always-repoint" policy):
//  ----------------------------------------------
//  Many vanilla items share a name/tooltip key BY VALUE. The old
//  injector OVERWROTE the string at the item's existing key, so it
//  changed every item sharing that key, and — worse — its
//  ClearPrevious() called Localization.LoadDefaultLanguage(), which
//  RELOADS the whole table from disk. When the injector ran a second
//  time (pre-apply + server payload), that reload WIPED everything,
//  so a repointed name reverted to a raw GUID on screen.
//
//  This patcher instead REPOINTS: it mints a brand-new AssetGuid
//  (unique by construction), writes the new string there, and points
//  the item's Name at it. No shared-key contamination, and crucially
//  NO LoadDefaultLanguage anywhere — nothing wipes our keys.
//
//  Lifecycle — mirrors IconPatcher exactly:
//  -----------------------------------------
//  SyncReceiver.ApplyPayload() calls ClearPrevious() then Apply() on
//  every payload, the same clear-then-reapply pattern IconPatcher
//  uses. ClearPrevious restores each item's captured original Name;
//  Apply re-captures (now-vanilla) originals, mints fresh keys, and
//  repoints. This is idempotent across the pre-apply + server-payload
//  double-apply.
//
//  Probe findings this is built on:
//  --------------------------------
//    ManagedItemData.Name : LocalizationKey  — public setter, VALUE
//      type, so assigning it persists (confirmed working in-game).
//    Mint:  AssetGuid.FromString(Guid.NewGuid().ToString())
//    Wrap:  new LocalizationKey(AssetGuid)  — confirmed public ctor.
//
//  TOOLTIPS ARE NOT HANDLED HERE — see the seam below. ManagedItemData
//  .Description is a REFERENCE property whose getter returns a copy;
//  assigning it does not persist (confirmed). Tooltips require a
//  Harmony patch on the tooltip-build path and are a separate task.
//
//  [CHANGED] RepointName() now passes display names through
//            ColorTranslator.Translate() before injecting into
//            _LocalizedStrings. V Rising named colour tags
//            (e.g. <teal1>, </c>) are converted to Unity rich text
//            (<color=#...>, </color>) at inject time so they render
//            correctly — injected strings bypass V Rising's tag
//            processing layer but Unity's render layer handles
//            rich text natively.
//
//  [PERFORMANCE] Per name: one dict write + one value-type field
//                assignment — O(1), microseconds, one-time at apply.
//                Steady state ZERO: once set, name lookup is identical
//                to vanilla (no getter patch, no per-frame work).
//                Note: each Apply mints fresh keys, leaving the prior
//                apply's string entries as harmless orphans in
//                _LocalizedStrings (a few per apply). String-entry
//                cleanup is deferred (Remove API unconfirmed); orphans
//                cost only trivial memory and are never referenced.
// ============================================================

namespace LilithsSoul.Services;

public static class LocalizationPatcher
{
    private const string LOG_SOURCE      = "LilithsSoul.LocalizationPatcher";
    private const string PrefabNamespace = "LilithsMind.Prefabs.Definitions";

    // prefab Name/Prefab string → PrefabGUID. Built once at world ready.
    // Mirrors IconPatcher's name map — the patcher resolves items by
    // PrefabGUID and reads their live Name key; it does NOT need the
    // recorded PrefabDef.NameKey (that requirement is gone for names).
    static readonly Dictionary<string, PrefabGUID> _nameToPrefabGuid
        = new(StringComparer.OrdinalIgnoreCase);

    // Captured original Name keys for restore, by prefab GuidHash.
    // Mirrors IconPatcher._previousIcons.
    static readonly Dictionary<int, LocalizationKey> _previousNames = new();

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Builds the prefab-name → PrefabGUID map from LilithsMind
    /// definitions. Called by SyncReceiver.NotifyWorldReady().
    /// Safe to call repeatedly — clears and rebuilds each call.
    /// [PERFORMANCE] O(n) reflection over LilithsMind definitions, once.
    /// </summary>
    public static void BuildNameMap()
    {
        _nameToPrefabGuid.Clear();

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

                // Guard both keys: Prefab is declared non-nullable, but a
                // malformed definition (or IL2CPP reflection read) can yield
                // null — a null Dictionary key throws. Skip + warn rather than
                // crash NotifyWorldReady (which would abort the whole apply).
                if (def.Prefab is not null)
                    _nameToPrefabGuid[def.Prefab] = guid;
                else
                    SoulLogger.Warning(LOG_SOURCE,
                        $"Definition {type.Name}.{field.Name} has null Prefab — skipped. Fix the entry.");

                if (def.Name is not null)
                    _nameToPrefabGuid[def.Name] = guid;
                count++;
            }
        }

        SoulLogger.Info(LOG_SOURCE,
            $"Name→PrefabGUID map built — {count} definition(s) from {definitionTypes.Count} class(es).");
    }

    /// <summary>
    /// Repoints item display names from payload.ItemAppearanceOverrides.
    /// Call AFTER ClearPrevious() on each payload (mirrors IconPatcher).
    /// Tooltip and Icon fields are intentionally ignored here.
    /// </summary>
    public static void Apply(ServerSyncPayload payload)
    {
        if (Soul.ClientWorld == null)
        {
            SoulLogger.Warning(LOG_SOURCE, "Client world not ready — cannot apply names.");
            return;
        }

        var registry = Soul.ClientWorld
            .GetExistingSystemManaged<ManagedDataSystem>()
            ?.ManagedDataRegistry;
        if (registry == null)
        {
            SoulLogger.Warning(LOG_SOURCE, "ManagedDataRegistry not available — cannot apply names.");
            return;
        }

        var table = Localization._LocalizedStrings;
        if (table == null)
        {
            SoulLogger.Warning(LOG_SOURCE, "_LocalizedStrings null — cannot apply names.");
            return;
        }

        int applied = 0;
        int skipped = 0;
        int noKeyOriginally = 0;

        foreach (var (prefabName, appearance) in payload.ItemAppearanceOverrides)
        {
            // Names only this pass. Tooltip-only / Icon-only entries are
            // handled by their respective services (Icon) or the future
            // tooltip Harmony patch — not here.
            if (appearance.DisplayName is null) continue;

            if (!_nameToPrefabGuid.TryGetValue(prefabName, out var guid))
            {
                SoulLogger.Warning(LOG_SOURCE,
                    $"No PrefabGUID for '{prefabName}' — add a stub PrefabDef in LilithsMind.");
                skipped++;
                continue;
            }

            var item = registry.GetOrDefault<ManagedItemData>(guid);
            if (item == null)
            {
                SoulLogger.Warning(LOG_SOURCE, $"No ManagedItemData for '{prefabName}' — skipping name.");
                skipped++;
                continue;
            }

            try
            {
                // [INFO] Track items that had an empty Name key originally — these
                //        are now renamable thanks to repointing, where the old
                //        injector would have skipped them (no recorded NameKey).
                if (item.Name.IsEmpty) noKeyOriginally++;

                RepointName(item, guid.GuidHash, appearance.DisplayName);
                applied++;
            }
            catch (Exception ex)
            {
                SoulLogger.Warning(LOG_SOURCE, $"Name repoint failed for '{prefabName}': {ex.Message}");
                skipped++;
            }
        }

        SoulLogger.Info(LOG_SOURCE,
            $"Names repointed — {applied} applied, {skipped} skipped"
          + (noKeyOriginally > 0 ? $" ({noKeyOriginally} had no original name key — newly renamable)." : "."));
    }

    /// <summary>
    /// Restores every repointed item's original Name. Called before each
    /// Apply (mirrors IconPatcher.ClearPrevious()). A repoint is not undone
    /// by anything else, so this explicit restore keeps re-applies clean and
    /// prevents a rename leaking across a server switch.
    /// </summary>
    public static void ClearPrevious()
    {
        if (_previousNames.Count == 0) return;

        if (Soul.ClientWorld == null) return;

        var registry = Soul.ClientWorld
            .GetExistingSystemManaged<ManagedDataSystem>()
            ?.ManagedDataRegistry;
        if (registry == null) return;

        int restored = 0;
        foreach (var (guidHash, originalName) in _previousNames)
        {
            var item = registry.GetOrDefault<ManagedItemData>(new PrefabGUID(guidHash));
            if (item == null) continue;

            try
            {
                item.Name = originalName;
                restored++;
            }
            catch (Exception ex)
            {
                SoulLogger.Warning(LOG_SOURCE, $"Failed to restore name for GUID {guidHash}: {ex.Message}");
            }
        }

        SoulLogger.Debug(LOG_SOURCE, $"Restored {restored} original name(s).");
        _previousNames.Clear();
    }

    // ── Internal ─────────────────────────────────────────────

    static void RepointName(ManagedItemData item, int guidHash, string newName)
    {
        // Re-read the table here so we never name its (IL2CPP) type — var infers it.
        var table = Localization._LocalizedStrings;

        _previousNames.TryAdd(guidHash, item.Name);   // capture vanilla (first wins)

        var g = Mint();                                // fresh, unique by construction

        // [CHANGED] Translate V Rising named colour tags (e.g. <teal1>, </c>)
        // to Unity rich text (<color=#...>, </color>) before injecting.
        // Injected strings bypass V Rising's tag processing layer but Unity's
        // render layer handles rich text natively.
        table[g] = ColorTranslator.Translate(newName); // inject translated string

        item.Name = MakeKey(g);                        // repoint via public value-type setter
    }

    // A fresh GUID is just a dictionary key — no entity backing needed
    // (unlike PrefabGUID) — so minting is always valid.
    static AssetGuid Mint() => AssetGuid.FromString(Guid.NewGuid().ToString());

    // Confirmed public ctor (probe v4): new LocalizationKey(AssetGuid).
    static LocalizationKey MakeKey(AssetGuid guid) => new LocalizationKey(guid);

    // ══════════════════════════════════════════════════════════
    //  TOOLTIP SEAM — deferred to a Harmony patch (separate task).
    //
    //  Tooltips CANNOT be repointed through ManagedItemData:
    //  .Description is a reference-type property whose getter returns
    //  a fresh copy, so assigning it does not persist (confirmed by
    //  read-back: re-getting Description after assign still resolved
    //  vanilla). Unlike Name (a value type), there is no storage to
    //  write. The fix is a Harmony patch on the tooltip-build call,
    //  substituting text/key at UI resolve time. When that lands, its
    //  ClearPrevious-equivalent and Apply hook should mirror this
    //  class's lifecycle. Until then, Tooltip overrides are no-ops.
    // ══════════════════════════════════════════════════════════
}