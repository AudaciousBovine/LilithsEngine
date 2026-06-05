// ============================================================
//  SoulConfig — LilithsSoul
//  LilithsSoul/Config/SoulConfig.cs
//
//  BepInEx config bindings for the Soul core.
//
//  [CHANGED] PreferredLanguage default changed from English to System.
//            System is a Soul-only sentinel — SystemLanguageResolver
//            reads Localization.CurrentLanguage from the running
//            V Rising client and maps it to a concrete language at
//            connect time. Players with no config file get automatic
//            language detection out of the box.
//
//            Players who want to override their game language (e.g.
//            play in English but receive Spanish item names) can set
//            this to any real LanguageCodeEnum value. If the server
//            has not configured that language, Soul stays on the
//            server default and logs a warning.
// ============================================================

using BepInEx.Configuration;
using LilithsMind.Data;
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

        // [CHANGED] Default changed from English → System.
        //           System instructs Soul to detect the language the
        //           V Rising client is currently running in, via
        //           Localization.CurrentLanguage. No manual config needed.
        //
        //           Set to a specific language (e.g. Spanish) to override
        //           your game language for server item names/descriptions.
        //           If the server has not configured that language, Soul
        //           stays on the server default and logs a warning.
        _preferredLanguage = config.Bind(
            section:      "2) Localization",
            key:          "PreferredLanguage",
            defaultValue: LanguageCodeEnum.System,
            description:  "Your preferred language for item names and descriptions. " +
                          "System (default) automatically detects the language your " +
                          "V Rising client is running in. Set to a specific language " +
                          "to override — e.g. Spanish, French, SChinese. " +
                          "If the server has not configured the requested language, " +
                          "the server's default language is used instead."
        );

        SoulLogger.Info(LOG_SOURCE,
            $"SoulConfig loaded. Debug={IsDebug}, PreferredLanguage={PreferredLanguage}");
    }
}