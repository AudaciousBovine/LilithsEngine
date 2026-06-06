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
            section:      "2) Debug",
            key:          "DebugLogging",
            defaultValue: false,
            description:  "Enable verbose debug logging for LilithsSoul. " +
                          "Useful during development — disable on live servers."
        );

        _preferredLanguage = config.Bind(
            section:      "1) Localization",
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