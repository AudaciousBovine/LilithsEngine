// ============================================================
//  PrefabNameResolver — LilithsHeart
//  LilithsHeart/Services/PrefabNameResolver.cs
//
//  Resolves prefab names to PrefabGUIDs and vice versa.
//
//  Two-phase initialization:
//  ──────────────────────────
//  Phase 1 — compiled defaults (unchanged):
//    Scans LilithsMind's assembly for all static PrefabDef fields.
//    Builds _nameToGuid, _prefabToGuid, _guidToName from the
//    Name/Prefab/GuidHash fields on each definition.
//
//  Phase 2 — admin alias overrides (new):
//    Scans Aliases/*.json after compiled defaults are loaded.
//    Each file is named after its index class (e.g. WeaponsIndex.json)
//    and maps prefab string → custom alias name:
//      { "Item_Weapon_Sword_T01_Bone": "BoneCleaver" }
//    For each valid entry:
//      • The old compiled Name is removed from _nameToGuid
//      • The new admin alias is inserted into _nameToGuid
//      • _guidToName is updated to return the admin alias
//      • _prefabToGuid is always preserved — the prefab string
//        fallback cannot be overridden (safety net)
//    Per-server: Aliases/ lives on the server under
//    BepInEx/config/LilithsHeart/Aliases/ — each server has
//    its own independent set of aliases. Soul never sees them.
//
//  GenerateAliasFiles() — triggered by GenerateNameAliasConfigs:
//    Dumps one JSON file per *Index class to Aliases/.
//    Each file contains the current compiled Name values so
//    admins have a starting point to edit. Always overwrites.
//
//  Safety rules for alias loading:
//    • Alias must not be null or whitespace
//    • Alias must not collide with an existing _prefabToGuid key
//      (reserved namespace — raw prefab strings always resolve)
//    • If invalid, the entry is logged and skipped
//
//  [CHANGED] Phase 2 alias loading added via LoadAliasOverrides().
//            GenerateAliasFiles() added for the dump-on-demand path.
//            Both called from Initialize() after Phase 1 completes.
//
//  [PERFORMANCE] Phase 1 reflection runs once at world ready — O(n).
//                Phase 2 file I/O runs once — O(alias files × entries).
//                All lookups remain O(1) dictionary reads.
// ============================================================

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stunlock.Core;
using LilithsHeart.Config;
using LilithsHeart.Foundation;
using LilithsMind.Prefabs;

namespace LilithsHeart.Services;

public static class PrefabNameResolver
{
    private const string LOG_SOURCE      = "LilithsHeart.PrefabNameResolver";
    private const string PrefabNamespace = "LilithsMind.Prefabs.Definitions";

    public static readonly PrefabGUID Empty = new(0);

    static readonly Dictionary<string, PrefabGUID> _nameToGuid   = new();
    static readonly Dictionary<string, PrefabGUID> _prefabToGuid = new();
    static readonly Dictionary<int, string>         _guidToName   = new();

    // Tracks the compiled Name for each GUID so Phase 2 can remove
    // the old name from _nameToGuid before inserting the admin alias.
    static readonly Dictionary<int, string?> _compiledName = new();

    // Per-index-class tracking: type name → list of (GuidHash, PrefabString, CompiledName)
    // Built in Phase 1, used by GenerateAliasFiles() to write one file per index class.
    static readonly Dictionary<string, List<(int GuidHash, string Prefab, string? Name)>>
        _entriesByIndexClass = new();

    static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── Public API ───────────────────────────────────────────

    public static void Initialize()
    {
        // Phase 1 — compiled defaults from LilithsMind reflection.
        LoadCompiledDefaults();

        // Phase 2 — admin alias overrides from Aliases/*.json.
        // Runs even if alias files don't exist yet (graceful skip).
        LoadAliasOverrides();

        // Alias file generation — triggered by HeartConfig flag.
        // Must run after Phase 1 so compiled data is available.
        if (HeartConfig.GenerateNameAliasConfigs)
        {
            GenerateAliasFiles();
            HeartConfig.DisableGenerateNameAliasConfigs();
        }
    }

    // ── Forward lookup (name → GUID) ─────────────────────────

    /// <summary>
    /// Resolves a name string to a PrefabGUID.
    /// Checks admin alias / compiled Name first (_nameToGuid),
    /// then raw prefab string (_prefabToGuid).
    /// </summary>
    public static bool TryResolve(string name, out PrefabGUID guid)
    {
        if (_nameToGuid.TryGetValue(name, out guid))
            return true;

        if (_prefabToGuid.TryGetValue(name, out guid))
            return true;

        guid = Empty;
        HeartLogger.Warning(LOG_SOURCE, $"Could not resolve prefab name: '{name}'");
        return false;
    }

    // ── Reverse lookup (GUID → name) ─────────────────────────

    /// <summary>
    /// Resolves a PrefabGUID to its current name (admin alias if set,
    /// compiled Name otherwise, Prefab string as last resort).
    /// </summary>
    public static bool TryResolveName(PrefabGUID guid, out string name)
    {
        if (_guidToName.TryGetValue(guid._Value, out name!))
            return true;

        name = string.Empty;
        return false;
    }

    // ── Phase 1 — compiled defaults ───────────────────────────

    static void LoadCompiledDefaults()
    {
        var mindAssembly = typeof(PrefabDef).Assembly;

        var definitionTypes = mindAssembly
            .GetTypes()
            .Where(t =>
                t.IsClass    &&
                t.IsAbstract &&
                t.IsSealed   &&
                t.Namespace == PrefabNamespace)
            .ToList();

        if (definitionTypes.Count == 0)
        {
            HeartLogger.Warning(LOG_SOURCE,
                "No definition classes found in LilithsMind — resolver will be empty.");
            return;
        }

        int total = 0;

        foreach (var type in definitionTypes)
        {
            var fields = type
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(PrefabDef));

            var indexEntries = new List<(int, string, string?)>();

            foreach (var field in fields)
            {
                var def  = (PrefabDef)field.GetValue(null)!;
                var guid = new PrefabGUID(def.GuidHash);

                if (def.Name is not null)
                    _nameToGuid[def.Name] = guid;

                if (!string.IsNullOrEmpty(def.Prefab))
                    _prefabToGuid[def.Prefab] = guid;

                _guidToName[def.GuidHash]   = def.Name ?? def.Prefab;
                _compiledName[def.GuidHash] = def.Name;

                indexEntries.Add((def.GuidHash, def.Prefab, def.Name));
                total++;
            }

            // Store entries grouped by index class name for alias file generation.
            if (indexEntries.Count > 0)
                _entriesByIndexClass[type.Name] = indexEntries;
        }

        HeartLogger.Info(LOG_SOURCE,
            $"Phase 1 complete — {total} compiled definition(s) from " +
            $"{definitionTypes.Count} index class(es).");
    }

    // ── Phase 2 — admin alias overrides ──────────────────────

    static void LoadAliasOverrides()
    {
        if (!Directory.Exists(HeartPathIndex.AliasesDir))
        {
            HeartLogger.Debug(LOG_SOURCE,
                "Aliases/ directory not found — no alias overrides applied.");
            return;
        }

        var files = Directory
            .GetFiles(HeartPathIndex.AliasesDir, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        if (files.Length == 0)
        {
            HeartLogger.Debug(LOG_SOURCE, "No alias files found in Aliases/.");
            return;
        }

        int overrideCount = 0;
        int skipCount     = 0;

        foreach (var file in files)
        {
            try
            {
                var json    = File.ReadAllText(file);
                var entries = JsonSerializer.Deserialize<Dictionary<string, string?>>(json, _readOptions);

                if (entries == null) continue;

                foreach (var (prefabString, adminAlias) in entries)
                {
                    // Skip comment/readme keys.
                    if (prefabString.StartsWith("_")) continue;

                    // Alias must be non-empty.
                    if (string.IsNullOrWhiteSpace(adminAlias))
                    {
                        HeartLogger.Warning(LOG_SOURCE,
                            $"[Aliases] '{prefabString}' has null/empty alias — skipping.");
                        skipCount++;
                        continue;
                    }

                    // Prefab string must be known.
                    if (!_prefabToGuid.TryGetValue(prefabString, out PrefabGUID guid))
                    {
                        HeartLogger.Warning(LOG_SOURCE,
                            $"[Aliases] '{prefabString}' not found in compiled definitions — skipping.");
                        skipCount++;
                        continue;
                    }

                    // Alias must not collide with any prefab string —
                    // the _prefabToGuid namespace is reserved as the safe fallback.
                    if (_prefabToGuid.ContainsKey(adminAlias))
                    {
                        HeartLogger.Warning(LOG_SOURCE,
                            $"[Aliases] Alias '{adminAlias}' for '{prefabString}' collides with " +
                            "an existing prefab string — skipping. Choose a different alias.");
                        skipCount++;
                        continue;
                    }

                    // Remove the old compiled Name from _nameToGuid if one existed.
                    if (_compiledName.TryGetValue(guid._Value, out var oldName) &&
                        oldName is not null)
                        _nameToGuid.Remove(oldName);

                    // Insert the admin alias.
                    _nameToGuid[adminAlias]     = guid;
                    _guidToName[guid._Value]    = adminAlias;

                    HeartLogger.Debug(LOG_SOURCE,
                        $"[Aliases] '{prefabString}' → '{adminAlias}'");
                    overrideCount++;
                }
            }
            catch (Exception ex)
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"Failed to read alias file '{Path.GetFileName(file)}': {ex.Message}");
            }
        }

        HeartLogger.Info(LOG_SOURCE,
            $"Phase 2 complete — {overrideCount} alias override(s) applied, " +
            $"{skipCount} skipped, from {files.Length} file(s).");
    }

    // ── Alias file generation ─────────────────────────────────

    /// <summary>
    /// Dumps one JSON file per *Index class to Aliases/.
    /// Each file maps prefab string → current compiled Name (or null
    /// if the entry has no Name). Admins edit the values to set
    /// custom aliases for this server. Always overwrites.
    ///
    /// Called from Initialize() when GenerateNameAliasConfigs = true.
    ///
    /// [PERFORMANCE] Runs once per flag trigger. O(definitions).
    ///               No ECS access needed — all data from Phase 1.
    /// </summary>
    static void GenerateAliasFiles()
    {
        Directory.CreateDirectory(HeartPathIndex.AliasesDir);

        int fileCount = 0;

        foreach (var (indexClassName, entries) in _entriesByIndexClass)
        {
            var path = Path.Combine(HeartPathIndex.AliasesDir, $"{indexClassName}.json");

            try
            {
                // Build output: prefab string → current Name (admin edits the value).
                // Null Name means the entry has no alias yet — admin can add one.
                var output = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["_readme"] =
                        $"Prefab name alias overrides for {indexClassName}. " +
                        "Keys are prefab strings (do not change). " +
                        "Values are the alias used in all module configs on this server. " +
                        "Set a value to null to use the prefab string directly. " +
                        "Aliases must not match any existing prefab string.",
                };

                foreach (var (guidHash, prefab, compiledName) in entries)
                    output[prefab] = compiledName ?? (object)"null";

                var json = JsonSerializer.Serialize(output, _writeOptions);
                File.WriteAllText(path, json);
                fileCount++;
            }
            catch (Exception ex)
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"Could not write alias file '{indexClassName}.json': {ex.Message}");
            }
        }

        HeartLogger.Info(LOG_SOURCE,
            $"Generated {fileCount} alias file(s) in Aliases/.");
    }
}