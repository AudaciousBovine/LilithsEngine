// ============================================================
//  LanguageCodeEnum — LilithsMind
//  LilithsMind/Data/LanguageCodeEnum.cs
//
//  Language codes supported by V Rising / Steam.
//  Used in HeartConfig.DefaultLanguage and SoulConfig.PreferredLanguage.
//  Folder names under Localization/ match the enum names exactly —
//  e.g. Localization/Spanish/, Localization/SChinese/.
//
//  Custom allows server admins to define a non-standard language
//  folder for private or modded language packs.
//
//  [CHANGED] System added — a Soul-only sentinel value that instructs
//            SystemLanguageResolver to read Localization.CurrentLanguage
//            from the running V Rising client and map it to a concrete
//            LanguageCodeEnum value at runtime. System is never sent
//            over the wire — Soul resolves it to a real language name
//            before sending [[LG:lang-request:X]] to Heart.
//            This is the new default for PreferredLanguage, meaning
//            players get automatic language detection with no config.
// ============================================================

namespace LilithsMind.Data;

public enum LanguageCodeEnum
{
    // ── Real language codes ─────────────────────────────────
    // These match V Rising / Steam language folder names exactly.
    // Heart uses these as folder names under Localization/.
    // Soul uses these as the resolved wire value in lang-request sentinels.

    English,
    Brazilian,
    French,
    German,
    Hungarian,
    Italian,
    Japanese,
    Koreana,
    Latam,
    Polish,
    Russian,
    SChinese,
    Spanish,
    TChinese,
    Thai,
    Turkish,
    Ukrainian,
    Vietnamese,

    // ── Special values ──────────────────────────────────────

    /// <summary>
    /// Server admins may define a non-standard language folder.
    /// e.g. Localization/Custom/ with hand-authored overrides.
    /// </summary>
    Custom,

    /// <summary>
    /// [CHANGED] Soul-only sentinel. When PreferredLanguage is System,
    /// SystemLanguageResolver reads Localization.CurrentLanguage from the
    /// running V Rising client at connect time and maps it to one of the
    /// real LanguageCodeEnum values above.
    ///
    /// NEVER used as a folder name under Localization/.
    /// NEVER sent in a [[LG:lang-request:X]] sentinel.
    /// Heart never sees or handles this value.
    /// </summary>
    System,
}