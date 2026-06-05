// ============================================================
//  SoulPathIndex — LilithsSoul
//  LilithsSoul/Config/SoulPathIndex.cs
//
//  Single source of truth for every filesystem path used by
//  LilithsSoul and its child client modules.
//
//  All Soul config lives under:
//      BepInEx/config/LilithsSoul/
//
//  Structure:
//      LilithsSoul/
//          LilithsSoul.cfg
//          servers.json                    ← connection string → folder name map
//          Icons/                          ← custom PNG icons + URL download cache
//          <ServerIdentity>/
//              sync.json                   ← cached ServerSyncPayload per server
//              localization_Spanish.json   ← cached localization payload per language
//              localization_French.json
//
//  [CHANGED] LocalizationFile() added — per-server, per-language
//            cached localization payloads. Pre-applied on reconnect
//            alongside sync.json before the UI builds.
// ============================================================

namespace LilithsSoul.Config;

public static class SoulPathIndex
{
    // ── Root ────────────────────────────────────────────────

    /// <summary>
    /// BepInEx/config/LilithsSoul/
    /// </summary>
    public static readonly string Root = Path.Combine(
        BepInEx.Paths.ConfigPath,
        "LilithsSoul"
    );

    // ── .cfg files ──────────────────────────────────────────

    /// <summary>
    /// BepInEx/config/LilithsSoul/LilithsSoul.cfg
    /// </summary>
    public static readonly string CoreConfig = Path.Combine(Root, "LilithsSoul.cfg");

    // ── Shared data ─────────────────────────────────────────

    /// <summary>
    /// BepInEx/config/LilithsSoul/Icons/
    /// Custom PNG icon files and URL download cache.
    /// Scanned recursively by IconPatcher.
    /// </summary>
    public static readonly string IconsDir = Path.Combine(Root, "Icons");

    // ── Per-server data ─────────────────────────────────────

    /// <summary>
    /// Returns the directory for a specific server's cached data.
    /// e.g. SoulPathIndex.ServerDir("LilithsEngine")
    ///      → BepInEx/config/LilithsSoul/LilithsEngine/
    /// </summary>
    public static string ServerDir(string serverIdentity)
        => Path.Combine(Root, serverIdentity);

    /// <summary>
    /// Returns the path to the cached sync payload for a specific server.
    /// e.g. SoulPathIndex.SyncFile("LilithsEngine")
    ///      → BepInEx/config/LilithsSoul/LilithsEngine/sync.json
    /// </summary>
    public static string SyncFile(string serverIdentity)
        => Path.Combine(ServerDir(serverIdentity), "sync.json");

    /// <summary>
    /// Returns the path to the cached localization payload for a specific
    /// server and language.
    /// e.g. SoulPathIndex.LocalizationFile("LilithsEngine", "Spanish")
    ///      → BepInEx/config/LilithsSoul/LilithsEngine/localization_Spanish.json
    ///
    /// [CHANGED] Added for multi-language localization support.
    /// Pre-applied on reconnect after sync.json before the UI builds.
    /// </summary>
    public static string LocalizationFile(string serverIdentity, string languageName)
        => Path.Combine(ServerDir(serverIdentity), $"localization_{languageName}.json");
}