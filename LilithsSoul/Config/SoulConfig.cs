// ============================================================
//  SoulConfig — LilithsSoul
//  LilithsSoul/Config/SoulConfig.cs
//
//  BepInEx config bindings for the Soul core.
//
//  [CHANGED] PreferredLanguage added — Soul compares this against
//            ServerSyncPayload.ServerLanguage on connect. If they
//            differ, Soul sends [[LG:lang-request:<language>]] to
//            Heart to request a localization payload for the
//            preferred language.
// ============================================================

using BepInEx.Configuration;
using LilithsMind.Data;    // SyncModeEnum, SyncTierEnum, LanguageCodeEnum
using LilithsMind.Network;
using LilithsSoul.Foundation;

namespace LilithsSoul.Config;

public static class SoulConfig
{
    private const string LOG_SOURCE = "LilithsSoul.SoulConfig";

    static ConfigEntry<bool>             _debugLogging      = null!;
    static ConfigEntry<LanguageCodeEnum> _preferredLanguage = null!;

    public static bool             IsDebug           => _debugLogging.Value;
    public static LanguageCodeEnum PreferredLanguage => _preferredLanguage.Value;

    public static void Initialize(ConfigFile config)
    {
        _debugLogging = config.Bind(
            section:      "1) Core",
            key:          "DebugLogging",
            defaultValue: false,
            description:  "Enable verbose debug logging for LilithsSoul. " +
                          "Useful during development, disable in production."
        );

        // [CHANGED] PreferredLanguage — if this differs from the server's
        //           DefaultLanguage, Soul will request a localization payload
        //           for this language after receiving the main sync payload.
        //           If the server has not configured this language, Soul stays
        //           on the server default and logs a warning.
        _preferredLanguage = config.Bind(
            section:      "2) Localization",
            key:          "PreferredLanguage",
            defaultValue: LanguageCodeEnum.English,
            description:  "Your preferred language for item names and descriptions. " +
                          "If this differs from the server's default language and the " +
                          "server has configured overrides for your language, they will " +
                          "be applied automatically on connect. " +
                          "If unavailable, the server's default language is used."
        );

        SoulLogger.Info(LOG_SOURCE,
            $"SoulConfig loaded. Debug={IsDebug}, PreferredLanguage={PreferredLanguage}");
    }
}