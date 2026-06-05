// ============================================================
//  SystemLanguageResolver — LilithsSoul
//  LilithsSoul/Services/SystemLanguageResolver.cs
//
//  Resolves LanguageCodeEnum.System to a concrete language name
//  by reading Localization.CurrentLanguage from the running
//  V Rising client.
//
//  Why this exists:
//  ─────────────────
//  LanguageCodeEnum.System is a Soul-only sentinel meaning
//  "use whatever language the game is currently running in."
//  Rather than scattering the resolution logic, this service
//  owns it in one place. SyncReceiver calls Resolve() once
//  when it needs to compare against ServerLanguage.
//
//  How it works:
//  ──────────────
//  Localization.CurrentLanguage is a public static string property
//  confirmed present in the V Rising IL2CPP assembly:
//      NativeMethodInfoPtr_get_CurrentLanguage_Public_Static_get_String_0
//  It returns a string matching the game's active language, e.g.
//  "English", "Spanish", "SChinese" — which map directly to
//  LanguageCodeEnum member names.
//
//  Mapping strategy:
//  ──────────────────
//  Enum.TryParse<LanguageCodeEnum> is used for the direct match.
//  V Rising uses the same Steamworks language names that our enum
//  is built on, so the match should always succeed for any
//  language V Rising officially supports.
//
//  If the game returns an unrecognised string (modded locale, future
//  language addition, or empty on a headless server), we fall back
//  to English and log a warning. This is safe — the server will
//  simply serve its default language.
//
//  Caching:
//  ─────────
//  The resolved language is cached after the first successful
//  resolution. Localization.CurrentLanguage is set once at game
//  start and does not change mid-session in V Rising. The cache
//  is reset on world teardown (Soul.Reset()) in case the player
//  restarts into a different language.
//
//  [PERFORMANCE] Resolve() after first call: one null check + one
//                field read — O(1), zero allocations. First call
//                only: one static property access + one TryParse —
//                negligible, called at most once per session.
// ============================================================

using Stunlock.Localization;    // Localization.CurrentLanguage
using LilithsMind.Data;
using LilithsSoul.Foundation;

namespace LilithsSoul.Services;

public static class SystemLanguageResolver
{
    private const string LOG_SOURCE = "LilithsSoul.SystemLanguageResolver";

    // [PERFORMANCE] Cached after first resolution — CurrentLanguage
    // does not change mid-session. Null signals "not yet resolved".
    static string? _cachedLanguage;

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Returns the concrete LanguageCodeEnum name string (e.g. "Spanish")
    /// that corresponds to the V Rising client's current active language.
    ///
    /// Falls back to "English" if the game language cannot be resolved
    /// or is not present in LanguageCodeEnum.
    ///
    /// [CHANGED] Added — called by SyncReceiver when PreferredLanguage
    ///           is LanguageCodeEnum.System, before sending a lang-request.
    /// </summary>
    public static string Resolve()
    {
        // Return cached result after first call.
        if (_cachedLanguage != null)
            return _cachedLanguage;

        _cachedLanguage = ResolveFromGame();
        return _cachedLanguage;
    }

    /// <summary>
    /// Clears the cached resolved language.
    /// Call from Soul.Reset() on world teardown so the next session
    /// re-reads from the game in case the player changed language.
    /// </summary>
    public static void Reset()
    {
        _cachedLanguage = null;
    }

    // ── Internal ─────────────────────────────────────────────

    static string ResolveFromGame()
    {
        string? gameLanguage = null;

        try
        {
            // [CHANGED] Localization.CurrentLanguage is a public static string
            // property confirmed in the V Rising IL2CPP assembly dump:
            //   NativeMethodInfoPtr_get_CurrentLanguage_Public_Static_get_String_0
            // Returns a string matching V Rising / Steamworks language names,
            // e.g. "English", "Spanish", "SChinese". These align directly
            // with our LanguageCodeEnum member names.
            gameLanguage = Localization.CurrentLanguage;
        }
        catch (Exception ex)
        {
            SoulLogger.Warning(LOG_SOURCE,
                $"Failed to read Localization.CurrentLanguage: {ex.Message} " +
                "— falling back to English.");
            return "English";
        }

        if (string.IsNullOrWhiteSpace(gameLanguage))
        {
            SoulLogger.Warning(LOG_SOURCE,
                "Localization.CurrentLanguage returned null or empty " +
                "— falling back to English.");
            return "English";
        }

        // Attempt a direct match against LanguageCodeEnum.
        // The enum member names mirror V Rising's Steamworks language strings exactly.
        if (Enum.TryParse<LanguageCodeEnum>(gameLanguage, ignoreCase: true, out var parsed) &&
            parsed != LanguageCodeEnum.System &&
            parsed != LanguageCodeEnum.Custom)
        {
            SoulLogger.Info(LOG_SOURCE,
                $"Resolved system language: '{gameLanguage}'.");
            return parsed.ToString();   // Normalised to enum member casing.
        }

        // Game returned a string we don't recognise — unknown future language,
        // modded locale, or headless server edge case.
        SoulLogger.Warning(LOG_SOURCE,
            $"Localization.CurrentLanguage '{gameLanguage}' does not match any " +
            "LanguageCodeEnum value — falling back to English.");
        return "English";
    }
}