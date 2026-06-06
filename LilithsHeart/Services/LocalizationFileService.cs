// ============================================================
//  LocalizationFileService — LilithsHeart
//  LilithsHeart/Services/LocalizationFileService.cs
//
//  Loads per-language item name and description overrides from
//  the Localization/ directory tree.
//
//  Directory structure:
//  ─────────────────────
//  BepInEx/config/LilithsHeart/Localization/
//      Spanish/
//          Items/
//              items-es.json
//              weapons-es.json
//      French/
//          Items/
//              items-fr.json
//      ...
//
//  [CHANGED] Item overrides now live in Localization/(Language)/Items/
//            rather than directly in Localization/(Language)/.
//            This separates item localization from future localization
//            categories (e.g. spells, quests) that will live alongside
//            Items/ as sibling subdirectories under each language folder.
//
//  Folder names must match LanguageCodeEnum member names exactly
//  (e.g. "Spanish", "SChinese", "Brazilian").
//  Files inside each Items/ folder are scanned recursively for *.json.
//  Format is the same as Items/ files — keys are prefab names,
//  values are LilithItemData objects. Only DisplayName and
//  DescriptionText are meaningful here — Icon and StackSize are
//  language-independent and ignored if present.
//
//  [PERFORMANCE] Scanned once at world ready. Results cached in
//                _languageOverrides — O(1) lookup per language.
//                No file I/O after initialization.
// ============================================================

using System.Text.Json;
using LilithsMind.Data;
using LilithsMind.Network;
using LilithsHeart.Config;
using LilithsHeart.Foundation;

namespace LilithsHeart.Services;

public static class LocalizationFileService
{
    private const string LOG_SOURCE = "LilithsHeart.LocalizationFileService";

    // Per-language override dictionaries.
    // Key: LanguageCodeEnum name string (e.g. "Spanish")
    // Value: prefab name → LilithItemData (DisplayName + DescriptionText only)
    static readonly Dictionary<string, Dictionary<string, LilithItemData>> _languageOverrides = new();

    static bool _initialized;

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Scans Localization/ subfolders and loads all language overrides.
    /// Called from Heart.OnInitialize() after ItemService.
    /// No-op if already initialized.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        var locDir = HeartPathIndex.LocalizationDir;

        if (!Directory.Exists(locDir))
        {
            HeartLogger.Info(LOG_SOURCE,
                "Localization/ directory not found — multi-language support disabled.");
            _initialized = true;
            return;
        }

        // Each subdirectory is a language code matching LanguageCodeEnum.
        var langDirs = Directory.GetDirectories(locDir);

        foreach (var langDir in langDirs)
        {
            var langName = Path.GetFileName(langDir);

            // Validate against LanguageCodeEnum.
            if (!Enum.TryParse<LanguageCodeEnum>(langName, out _))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"Localization subfolder '{langName}' does not match any LanguageCodeEnum " +
                    "value — skipping. Folder names must match exactly (e.g. 'Spanish', 'SChinese').");
                continue;
            }

            // [CHANGED] Item overrides live in Localization/(Language)/Items/
            // rather than directly in the language folder. This allows future
            // localization categories (spells, quests, etc.) to sit alongside
            // Items/ as sibling subdirectories without mixing file types.
            var itemsDir = Path.Combine(langDir, "Items");

            var overrides = LoadLanguageItemsDir(itemsDir, langName);

            if (overrides.Count > 0)
            {
                _languageOverrides[langName] = overrides;
                HeartLogger.Info(LOG_SOURCE,
                    $"Loaded {overrides.Count} override(s) for language '{langName}'.");
            }
        }

        HeartLogger.Info(LOG_SOURCE,
            $"Localization initialized — {_languageOverrides.Count} language(s) available: " +
            string.Join(", ", _languageOverrides.Keys));

        _initialized = true;
    }

    /// <summary>
    /// Returns true if the given language has overrides configured.
    /// </summary>
    public static bool HasLanguage(string languageName)
        => _languageOverrides.ContainsKey(languageName);

    /// <summary>
    /// Returns the available language names (matching LanguageCodeEnum).
    /// </summary>
    public static IReadOnlyCollection<string> AvailableLanguages
        => _languageOverrides.Keys;

    /// <summary>
    /// Builds a ServerSyncPayload containing only the DisplayName and
    /// DescriptionText overrides for the requested language.
    /// Returns null if the language is not available.
    ///
    /// The payload reuses ServerSyncPayload with only ItemAppearanceOverrides
    /// populated — Soul's existing ApplyTier() handles it naturally,
    /// overwriting only DisplayName and DescriptionText, leaving Icon untouched.
    /// </summary>
    public static ServerSyncPayload? BuildLocalizationPayload(
        string serverIdentity,
        string languageName)
    {
        if (!_languageOverrides.TryGetValue(languageName, out var overrides))
            return null;

        // Build a payload with only the localization slice populated.
        // Icon is intentionally excluded — language does not affect icons.
        var appearance = overrides
            .Where(kvp =>
                kvp.Value.DisplayName is not null ||
                kvp.Value.DescriptionText is not null)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => new LilithItemData
                {
                    DisplayName     = kvp.Value.DisplayName,
                    DescriptionText = kvp.Value.DescriptionText,
                    // Icon and StackSize intentionally omitted.
                });

        return new ServerSyncPayload
        {
            ServerIdentity          = serverIdentity,
            ServerLanguage          = languageName,
            ItemAppearanceOverrides = appearance,
        };
    }

    // ── Internal ─────────────────────────────────────────────

    /// <summary>
    /// Loads item overrides from Localization/(Language)/Items/ recursively.
    /// Returns an empty dictionary if the Items/ subdirectory does not exist.
    /// </summary>
    static Dictionary<string, LilithItemData> LoadLanguageItemsDir(
        string itemsDir, string langName)
    {
        var result = new Dictionary<string, LilithItemData>(StringComparer.Ordinal);

        // [CHANGED] Items/ subdirectory is now required under each language folder.
        // If it doesn't exist the language is effectively unconfigured for items.
        if (!Directory.Exists(itemsDir))
        {
            HeartLogger.Debug(LOG_SOURCE,
                $"Localization/{langName}/Items/ not found — no item overrides for '{langName}'.");
            return result;
        }

        var files = Directory
            .GetFiles(itemsDir, "*.json", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        if (files.Length == 0)
        {
            HeartLogger.Debug(LOG_SOURCE,
                $"No JSON files found in Localization/{langName}/Items/ — skipping.");
            return result;
        }

        var readOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var raw  = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, readOptions);

                if (raw == null) continue;

                foreach (var (key, element) in raw)
                {
                    if (element.ValueKind != JsonValueKind.Object) continue;

                    string? displayName     = null;
                    string? descriptionText = null;

                    if (element.TryGetProperty("DisplayName", out var dn) &&
                        dn.ValueKind == JsonValueKind.String)
                        displayName = dn.GetString();

                    if (element.TryGetProperty("DescriptionText", out var dt) &&
                        dt.ValueKind == JsonValueKind.String)
                        descriptionText = dt.GetString();

                    if (displayName is null && descriptionText is null) continue;

                    if (!result.TryGetValue(key, out var existing))
                    {
                        result[key] = new LilithItemData
                        {
                            DisplayName     = displayName,
                            DescriptionText = descriptionText,
                        };
                    }
                    else
                    {
                        // Per-field merge — later file wins.
                        if (displayName     is not null) existing.DisplayName     = displayName;
                        if (descriptionText is not null) existing.DescriptionText = descriptionText;
                    }
                }
            }
            catch (Exception ex)
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"Failed to read '{Path.GetFileName(file)}' for language '{langName}': {ex.Message}");
            }
        }

        return result;
    }
}