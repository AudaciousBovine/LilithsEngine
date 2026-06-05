using System.Text.Json;
using LilithsSoul.Foundation;

// ============================================================
//  ServerRegistry — LilithsSoul
//  LilithsSoul/Config/ServerRegistry.cs
//
//  Maintains a mapping of server connection strings to their
//  LilithsSoul sync folder names, stored in:
//      BepInEx/config/LilithsSoul/servers.json
//
//  This allows Soul to pre-apply the cached sync.json from
//  disk BEFORE connecting to the server and BEFORE CharacterHUD
//  builds — eliminating the UI timing race condition.
//
//  File format:
//  {
//    "127.0.0.1:9876": "LilithsEngine",
//    "myserver.com:9876": "BloodcraftServer"
//  }
//
//  The key is ClientBootstrapSystem.ConnectionString.
//  The value is ServerSyncPayload.ServerIdentity (already
//  sanitized by Heart before sending).
//
//  [PERFORMANCE] File is read once on world ready and cached
//                in memory. Writes only occur when a new
//                server is encountered or identity changes.
// ============================================================

namespace LilithsSoul.Config;

public static class ServerRegistry
{
    private const string LOG_SOURCE = "LilithsSoul.ServerRegistry";

    static readonly string FilePath = Path.Combine(SoulPathIndex.Root, "servers.json");

    static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented = true
    };

    // In-memory cache — loaded once per world ready.
    static Dictionary<string, string>? _map;

    // ── Public API ────────────────────────────────────────────

    /// <summary>
    /// Loads servers.json from disk into memory.
    /// Call once at world ready before attempting any lookups.
    /// Safe to call multiple times — reloads from disk each time.
    ///
    /// [PERFORMANCE] One disk read per world ready — negligible.
    /// </summary>
    public static void Load()
    {
        if (!File.Exists(FilePath))
        {
            _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SoulLogger.Debug(LOG_SOURCE, "servers.json not found — starting empty.");
            return;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            _map = JsonSerializer.Deserialize<Dictionary<string, string>>(json, _readOptions)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            SoulLogger.Info(LOG_SOURCE,
                $"Loaded servers.json — {_map.Count} server(s) registered.");
        }
        catch (Exception ex)
        {
            SoulLogger.Warning(LOG_SOURCE,
                $"Failed to read servers.json: {ex.Message} — starting empty.");
            _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Attempts to resolve a connection string to a server folder name.
    /// Returns false if the connection string is not registered.
    ///
    /// [PERFORMANCE] O(1) dictionary lookup.
    /// </summary>
    public static bool TryGetFolderName(string connectionString, out string folderName)
    {
        folderName = string.Empty;

        if (_map == null)
        {
            SoulLogger.Warning(LOG_SOURCE, "ServerRegistry not loaded — call Load() first.");
            return false;
        }

        if (string.IsNullOrEmpty(connectionString)) return false;

        return _map.TryGetValue(connectionString, out folderName!);
    }

    /// <summary>
    /// Registers or updates the mapping from connection string to folder name.
    /// Persists the updated map to disk immediately.
    ///
    /// Called by SyncReceiver when a new payload arrives from a server,
    /// so future connects can pre-apply the cached sync without waiting
    /// for the server payload.
    ///
    /// [PERFORMANCE] One disk write per new server encounter — negligible.
    /// </summary>
    public static void Register(string connectionString, string folderName)
    {
        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(folderName))
            return;

        _map ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (_map.TryGetValue(connectionString, out var existing) && existing == folderName)
        {
            SoulLogger.Debug(LOG_SOURCE,
                $"'{connectionString}' already maps to '{folderName}' — no update needed.");
            return;
        }

        _map[connectionString] = folderName;
        Save();

        SoulLogger.Info(LOG_SOURCE,
            $"Registered '{connectionString}' → '{folderName}'.");
    }

    // ── Internal ─────────────────────────────────────────────

    static void Save()
    {
        try
        {
            Directory.CreateDirectory(SoulPathIndex.Root);
            var json = JsonSerializer.Serialize(_map, _writeOptions);
            File.WriteAllText(FilePath, json);
            SoulLogger.Debug(LOG_SOURCE, "servers.json saved.");
        }
        catch (Exception ex)
        {
            SoulLogger.Warning(LOG_SOURCE, $"Failed to save servers.json: {ex.Message}");
        }
    }
}