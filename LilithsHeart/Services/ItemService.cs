// ============================================================
//  ItemService — LilithsHeart
//  LilithsHeart/Services/ItemService.cs
//
//  Single owner of all Items/*.json file I/O.
//  Scans registered directories, parses JSON, and populates
//  LilithItemConfig in one pass per file.
//
//  All fields on LilithItemData are parsed here regardless of
//  which service owns them — the file is the single source of
//  truth and ItemService is the single reader of that file.
//  Each downstream service reads from LilithItemConfig:
//    LocalizationService  — DisplayName, DescriptionText
//    InterfaceService     — Icon
//    ItemFunctionService  — StackSize (LilithsCookbook)
//
//  [CHANGED] Replaces LocalizationService's file loading concern.
//            RegisterDirectory(), Load(), LoadFile() moved here.
//            StackSize parsed alongside appearance fields in one
//            pass — all go into the same LilithItemData entry via
//            LilithItemConfig.AddOverride().
//
//  [PERFORMANCE] All file I/O runs once at world ready.
//                No per-frame cost. O(files) I/O, O(entries) merge.
// ============================================================

using System.Text.Json;
using LilithsMind.Data;
using LilithsHeart.Config;
using LilithsHeart.Foundation;

namespace LilithsHeart.Services;

public static class ItemService
{
    private const string LOG_SOURCE = "LilithsHeart.ItemService";

    static readonly List<string> _registeredDirectories = [];

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Registers a directory to be scanned for *.json item override files.
    /// Must be called before Initialize() to take effect on first load.
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
    /// Creates registered directories, scans for *.json files, and
    /// populates LilithItemConfig.
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
                "No item override JSON files found — overrides disabled.");
            LilithItemConfig.MarkLoaded();
            return;
        }

        foreach (var file in files)
            LoadFile(file);

        LilithItemConfig.MarkLoaded();

        int total      = LilithItemConfig.Overrides.Count;
        int withStack  = LilithItemConfig.Overrides.Count(kvp => kvp.Value.StackSize.HasValue);

        HeartLogger.Info(LOG_SOURCE,
            $"Loaded {total} item override(s) from {files.Length} file(s) — " +
            $"{withStack} with StackSize.");
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

            int count = 0;

            foreach (var (key, element) in raw)
            {
                // Skip non-object values (e.g. _readme, _comment strings).
                if (element.ValueKind != JsonValueKind.Object) continue;

                string? displayName     = null;
                string? descriptionText = null;
                string? icon            = null;
                int?    stackSize       = null;

                // ── Appearance fields ─────────────────────────────────────────
                if (element.TryGetProperty("DisplayName", out var dn) &&
                    dn.ValueKind == JsonValueKind.String)
                    displayName = dn.GetString();

                if (element.TryGetProperty("DescriptionText", out var dt) &&
                    dt.ValueKind == JsonValueKind.String)
                    descriptionText = dt.GetString();

                if (element.TryGetProperty("Icon", out var ic) &&
                    ic.ValueKind == JsonValueKind.String)
                    icon = ic.GetString();

                // ── Functional fields ─────────────────────────────────────────
                // StackSize is owned by LilithsCookbook's ItemFunctionService.
                // Parsed here because ItemService owns all file I/O — downstream
                // services read from LilithItemConfig, never from files directly.
                if (element.TryGetProperty("StackSize", out var ss) &&
                    ss.ValueKind == JsonValueKind.Number &&
                    ss.TryGetInt32(out int stackSizeValue))
                    stackSize = stackSizeValue;

                // Skip entirely empty entries.
                if (displayName is null && descriptionText is null &&
                    icon is null && stackSize is null) continue;

                LilithItemConfig.AddOverride(key, new LilithItemData
                {
                    DisplayName     = displayName,
                    DescriptionText = descriptionText,
                    Icon            = icon,
                    StackSize       = stackSize,
                });

                count++;
            }

            HeartLogger.Info(LOG_SOURCE,
                $"Loaded '{Path.GetFileName(filePath)}' — {count} item override(s).");
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE,
                $"Failed to parse '{Path.GetFileName(filePath)}': {ex.Message}");
        }
    }
}