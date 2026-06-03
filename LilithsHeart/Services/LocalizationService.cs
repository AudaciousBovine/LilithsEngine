// ============================================================
//  LocalizationService — LilithsHeart
//  LilithsHeart/Services/LocalizationService.cs
//
//  Loads all Items/*.json files and populates LilithItemConfig.
//  Owns the file I/O and JSON parsing for all item overrides.
//
//  Supports multiple registered directories — each module calls
//  RegisterDirectory() to add its own config folder. Heart
//  registers ItemsDir; future modules register their own:
//    MainQuest/  — LilithsMachinations
//    Spells/     — LilithsGrimoire
//
//  Each registered directory is scanned recursively for *.json.
//  All files sorted by full path alphabetically, merged in order.
//  Later files win on a per-field basis via LilithItemConfig.Add*Override().
//
//  [CHANGED] ItemAppearanceConfig → LilithItemConfig throughout.
//            Now populates two separate dictionaries in one file
//            pass — appearance fields go to AddAppearanceOverride(),
//            functional fields go to AddFunctionalOverride().
//            Each service reads from its own dictionary; this service
//            owns only the loading concern.
//
//  [PERFORMANCE] All files read once at world ready. No file I/O
//                after initialization unless Reload() is called.
//                Per-field merge in LilithItemConfig is O(1).
// ============================================================

using System.Text.Json;
using LilithsMind.Data;
using LilithsHeart.Config;
using LilithsHeart.Foundation;

namespace LilithsHeart.Services;

public static class LocalizationService
{
    private const string LOG_SOURCE = "LilithsHeart.LocalizationService";

    static readonly List<string> _registeredDirectories = [];

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Registers a directory to be scanned for *.json override files.
    /// Must be called before Initialize() to take effect on first load.
    /// Child modules call this in their Load() or OnInitialized handler.
    /// Missing directories are skipped gracefully.
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
    /// into LilithItemConfig.
    /// Called once by Heart.OnInitialize() after HeartConfigBuilder runs.
    /// </summary>
    public static void Initialize()
    {
        foreach (var dir in _registeredDirectories)
            Directory.CreateDirectory(dir);

        Load();
    }

    /// <summary>
    /// Clears LilithItemConfig and reloads all files from all registered
    /// directories. Notifies Heart so the sync payload is rebuilt.
    /// Called by admin reload commands.
    /// </summary>
    public static void Reload()
    {
        LilithItemConfig.Clear();
        HeartLogger.Info(LOG_SOURCE, "Reloading item overrides...");
        Load();
        Heart.OnLocalizationReloaded();
    }

    // ── Internal ─────────────────────────────────────────────

    static void Load()
    {
        var files = _registeredDirectories
            .Where(Directory.Exists)
            .SelectMany(dir =>
                Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        if (files.Length == 0)
        {
            HeartLogger.Info(LOG_SOURCE,
                "No item override JSON files found in any registered directory — " +
                "overrides disabled.");
            LilithItemConfig.MarkLoaded();
            return;
        }

        foreach (var file in files)
            LoadFile(file);

        LilithItemConfig.MarkLoaded();

        HeartLogger.Info(LOG_SOURCE,
            $"Loaded {LilithItemConfig.AppearanceOverrides.Count} appearance override(s) " +
            $"and {LilithItemConfig.FunctionalOverrides.Count} functional override(s) " +
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
                    $"'{Path.GetFileName(filePath)}' parsed as null — check for malformed JSON.");
                return;
            }

            int appearanceCount  = 0;
            int functionalCount  = 0;

            foreach (var (key, element) in raw)
            {
                // Skip non-object values (e.g. _readme, _comment strings).
                if (element.ValueKind != JsonValueKind.Object) continue;

                // ── Appearance fields ─────────────────────────────────────────
                // Owned by LocalizationService (DisplayName, DescriptionText)
                // and InterfaceService (Icon). Stored together in LilithItemData
                // since they travel together in the sync payload.

                string? displayName     = null;
                string? descriptionText = null;
                string? icon            = null;

                if (element.TryGetProperty("DisplayName", out var dn) &&
                    dn.ValueKind == JsonValueKind.String)
                    displayName = dn.GetString();

                if (element.TryGetProperty("DescriptionText", out var dt) &&
                    dt.ValueKind == JsonValueKind.String)
                    descriptionText = dt.GetString();

                if (element.TryGetProperty("Icon", out var ic) &&
                    ic.ValueKind == JsonValueKind.String)
                    icon = ic.GetString();

                if (displayName is not null || descriptionText is not null || icon is not null)
                {
                    LilithItemConfig.AddAppearanceOverride(key, new LilithItemData
                    {
                        DisplayName     = displayName,
                        DescriptionText = descriptionText,
                        Icon            = icon,
                    });
                    appearanceCount++;
                }

                // ── Functional fields ─────────────────────────────────────────
                // Owned by ItemFunctionalService (StackSize).
                // Server-side only — never synced to Soul.

                int? stackSize = null;

                if (element.TryGetProperty("StackSize", out var ss) &&
                    ss.ValueKind == JsonValueKind.Number &&
                    ss.TryGetInt32(out int stackSizeValue))
                    stackSize = stackSizeValue;

                if (stackSize.HasValue)
                {
                    LilithItemConfig.AddFunctionalOverride(key, new LilithItemFunctionalData
                    {
                        StackSize = stackSize,
                    });
                    functionalCount++;
                }
            }

            HeartLogger.Info(LOG_SOURCE,
                $"Loaded '{Path.GetFileName(filePath)}' — " +
                $"{appearanceCount} appearance, {functionalCount} functional override(s).");
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE,
                $"Failed to parse '{Path.GetFileName(filePath)}': {ex.Message}");
        }
    }
}