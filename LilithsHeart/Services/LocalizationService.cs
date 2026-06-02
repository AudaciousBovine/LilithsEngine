using System.Text.Json;
using LilithsMind.Data;
using LilithsHeart.Config;
using LilithsHeart.Foundation;

// ============================================================
//  LocalizationService — LilithsHeart
//  LilithsHeart/Services/LocalizationService.cs
//
//  Central service responsible for loading all localization and
//  appearance overrides across the entire LilithsGarden suite.
//
//  Supports multiple registered directories — each module calls
//  RegisterDirectory() to add its own config folder. Heart
//  registers ItemsDir; future modules register their own:
//    MainQuest/  — LilithsMachinations
//    Spells/     — LilithsGrimoire
//
//  Each registered directory is scanned recursively for *.json.
//  All files sorted by full path alphabetically, merged in order.
//  Later files win on a per-field basis via ItemAppearanceConfig.AddOverride().
//
//  [CHANGED] Example file generation removed entirely — moved to
//            HeartConfigBuilder. LocalizationService is a loader
//            only. HeartConfigBuilder handles all example/generation
//            concerns, called from Heart.OnInitialize() before
//            this service initializes.
//
//  [CHANGED] GenerateLocalizationExample check removed from
//            Initialize() — HeartConfigBuilder now owns this.
//
//  [PERFORMANCE] All files read once at world ready. No file I/O
//                after initialization unless Reload() is called.
//                Per-field merge in ItemAppearanceConfig.AddOverride() is O(1).
// ============================================================

namespace LilithsHeart.Services;

public static class LocalizationService
{
    private const string LOG_SOURCE = "LilithsHeart.LocalizationService";

    // Registered directories to scan. Populated before Initialize() fires.
    // [PERFORMANCE] Small list — iteration is negligible.
    static readonly List<string> _registeredDirectories = [];

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Registers a directory to be scanned for *.json override files.
    /// Must be called before Initialize() to take effect on first load.
    /// Child modules call this in their Load() or OnInitialized handler.
    /// Directory does not need to exist yet — missing dirs are skipped gracefully.
    /// </summary>
    public static void RegisterDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!_registeredDirectories.Contains(path))
        {
            _registeredDirectories.Add(path);
            HeartLogger.Debug(LOG_SOURCE, $"Registered directory: '{path}'");
        }
    }

    /// <summary>
    /// Creates registered directories and loads all override files
    /// into ItemAppearanceConfig.
    /// Called once by Heart.OnInitialize() after HeartConfigBuilder runs.
    /// Example file generation is handled by HeartConfigBuilder — not here.
    /// </summary>
    public static void Initialize()
    {
        foreach (var dir in _registeredDirectories)
            Directory.CreateDirectory(dir);

        Load();
    }

    /// <summary>
    /// Clears ItemAppearanceConfig and reloads all files from all registered
    /// directories. Notifies Heart so the sync payload is rebuilt.
    /// Called by admin reload commands.
    /// </summary>
    public static void Reload()
    {
        ItemAppearanceConfig.Clear();
        HeartLogger.Info(LOG_SOURCE, "Reloading localization overrides...");
        Load();
        Heart.OnLocalizationReloaded();
    }

    // ── Internal ─────────────────────────────────────────────

    static void Load()
    {
        // Collect all *.json files from all registered directories recursively.
        // Sort by full path alphabetically so merge order is consistent.
        var files = _registeredDirectories
            .Where(Directory.Exists)
            .SelectMany(dir =>
                Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        if (files.Length == 0)
        {
            HeartLogger.Info(LOG_SOURCE,
                "No localization JSON files found in any registered directory — " +
                "overrides disabled.");
            ItemAppearanceConfig.MarkLoaded();
            return;
        }

        foreach (var file in files)
            LoadFile(file);

        ItemAppearanceConfig.MarkLoaded();

        HeartLogger.Info(LOG_SOURCE,
            $"Loaded {ItemAppearanceConfig.Overrides.Count} item appearance override(s) " +
            $"from {files.Length} file(s) across {_registeredDirectories.Count} directory(s).");
    }

    static void LoadFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var raw  = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (raw == null)
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"'{Path.GetFileName(filePath)}' parsed as null — " +
                    "check for malformed JSON.");
                return;
            }

            int count = 0;

            foreach (var (key, element) in raw)
            {
                // Skip non-object values (e.g. _readme, _comment strings).
                if (element.ValueKind != JsonValueKind.Object) continue;

                string? displayName = null;
                string? descriptionText     = null;
                string? icon        = null;

                if (element.TryGetProperty("DisplayName", out var dn) &&
                    dn.ValueKind == JsonValueKind.String)
                    displayName = dn.GetString();

                if (element.TryGetProperty("DescriptionText", out var tt) &&
                    tt.ValueKind == JsonValueKind.String)
                    descriptionText = tt.GetString();

                if (element.TryGetProperty("Icon", out var ic) &&
                    ic.ValueKind == JsonValueKind.String)
                    icon = ic.GetString();

                // Skip entirely empty entries.
                if (displayName is null && descriptionText is null && icon is null) continue;

                ItemAppearanceConfig.AddOverride(key, new ItemAppearanceData
                {
                    DisplayName = displayName,
                    DescriptionText     = descriptionText,
                    Icon        = icon,
                });

                count++;
            }

            HeartLogger.Info(LOG_SOURCE,
                $"Loaded '{Path.GetFileName(filePath)}' — {count} override(s).");
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE,
                $"Failed to parse '{Path.GetFileName(filePath)}': {ex.Message}");
        }
    }
}