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

    // [CHANGED] Raw GuidHash → PrefabGUID lookup.
    // Allows configs to use the raw integer hash as a key.
    // GuidHash values are signed ints and may be negative.
    static readonly Dictionary<int, PrefabGUID> _hashToGuid = new();

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
    /// Resolves a config key string to a PrefabGUID.
    /// Resolution order:
    ///   1. Admin alias or compiled Name  (_nameToGuid)
    ///   2. Raw prefab string             (_prefabToGuid)
    ///   3. Raw GuidHash integer string   (_hashToGuid)
    ///
    /// [CHANGED] Third lookup path added — admins can now use the
    ///           raw GuidHash integer (e.g. "-1595790789") as a key
    ///           in any config file, useful for items not yet defined
    ///           in LilithsMind.
    /// </summary>
    public static bool TryResolve(string name, out PrefabGUID guid)
    {
        // 1. Alias / compiled Name
        if (_nameToGuid.TryGetValue(name, out guid))
            return true;

        // 2. Raw prefab string
        if (_prefabToGuid.TryGetValue(name, out guid))
            return true;

        // 3. Raw GuidHash integer string
        // [CHANGED] GuidHash values are signed ints — use int.TryParse.
        if (int.TryParse(name, out int hash) &&
            _hashToGuid.TryGetValue(hash, out guid))
            return true;

        guid = Empty;
        HeartLogger.Warning(LOG_SOURCE,
            $"Could not resolve prefab name: '{name}' " +
            "(tried alias, prefab string, and GuidHash)");
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

                // [CHANGED] Also register by raw GuidHash for direct integer lookup.
                _hashToGuid[def.GuidHash] = guid;

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

    /// <summary>
    /// Loads alias overrides from Aliases/*.json.
    ///
    /// [CHANGED] File format changed from prefabString → alias to
    ///           GuidHash (int) → alias. Keys are now raw integer
    ///           GuidHash values (e.g. "862477668" or "-1595790789").
    ///           This removes the dependency on prefab strings being
    ///           stable across game updates and allows aliasing any
    ///           prefab by its GUID directly.
    ///
    ///           Old format (no longer supported):
    ///             "Item_BloodEssence_T01": "BloodEssence"
    ///           New format:
    ///             "862477668": "BloodEssence"
    ///
    ///           PrefabString keys that fail int.TryParse are skipped
    ///           with a warning so admins know to update their files.
    /// </summary>
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

                foreach (var (keyStr, adminAlias) in entries)
                {
                    // Skip comment/readme keys.
                    if (keyStr.StartsWith("_")) continue;

                    // Alias must be non-empty.
                    if (string.IsNullOrWhiteSpace(adminAlias))
                    {
                        HeartLogger.Warning(LOG_SOURCE,
                            $"[Aliases] '{keyStr}' has null/empty alias — skipping.");
                        skipCount++;
                        continue;
                    }

                    // [CHANGED] Key must be a valid GuidHash integer.
                    // Old prefab string keys (e.g. "Item_BloodEssence_T01") will
                    // fail here — admins must regenerate alias files to get the
                    // new GuidHash-keyed format.
                    if (!int.TryParse(keyStr, out int guidHash))
                    {
                        HeartLogger.Warning(LOG_SOURCE,
                            $"[Aliases] Key '{keyStr}' is not a valid GuidHash integer — skipping. " +
                            "Regenerate alias files with GenerateNameAliasConfigs=true to get the " +
                            "new GuidHash-keyed format.");
                        skipCount++;
                        continue;
                    }

                    // GuidHash must be known.
                    if (!_hashToGuid.TryGetValue(guidHash, out PrefabGUID guid))
                    {
                        HeartLogger.Warning(LOG_SOURCE,
                            $"[Aliases] GuidHash '{guidHash}' not found in compiled definitions — skipping.");
                        skipCount++;
                        continue;
                    }

                    // Alias must not collide with any prefab string —
                    // the _prefabToGuid namespace is reserved as the safe fallback.
                    if (_prefabToGuid.ContainsKey(adminAlias))
                    {
                        HeartLogger.Warning(LOG_SOURCE,
                            $"[Aliases] Alias '{adminAlias}' for GuidHash '{guidHash}' collides with " +
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
                        $"[Aliases] GuidHash '{guidHash}' → '{adminAlias}'");
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
    ///
    /// [CHANGED] Generated files now use GuidHash integer as the key
    ///           rather than prefab string. This makes aliases stable
    ///           across game updates where prefab string names may change,
    ///           and matches the new LoadAliasOverrides() format.
    ///
    ///           New format:
    ///             "862477668": "BloodEssence"    ← GuidHash → alias
    ///
    ///           Old format (retired):
    ///             "Item_BloodEssence_T01": "BloodEssence"
    ///
    /// Admins edit the values to set custom aliases for this server.
    /// Always overwrites. Called from Initialize() when
    /// GenerateNameAliasConfigs = true.
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
                var output = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["_readme"] =
                        $"Prefab alias overrides for {indexClassName}. " +
                        "Keys are GuidHash integers (do not change). " +
                        "Values are the alias used in all module configs on this server. " +
                        "Set a value to null to use the default compiled Name. " +
                        "Aliases must not match any existing prefab string.",
                };

                // [CHANGED] Key is now GuidHash integer string, not prefab string.
                // Value is the compiled Name (alias default) or null if none.
                foreach (var (guidHash, prefab, compiledName) in entries)
                    output[guidHash.ToString()] = compiledName ?? (object)"null";

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