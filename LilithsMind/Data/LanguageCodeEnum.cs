// ============================================================
//  LanguageCodeEnum — LilithsMind
//  LilithsMind/Network/LanguageCodeEnum.cs
//
//  Language codes supported by V Rising / Steam.
//  Used in HeartConfig.DefaultLanguage and SoulConfig.PreferredLanguage.
//  Folder names under Localization/ match the enum names exactly —
//  e.g. Localization/Spanish/, Localization/SChinese/.
//
//  Custom allows server admins to define a non-standard language
//  folder for private or modded language packs.
// ============================================================

namespace LilithsMind.Data;

public enum LanguageCodeEnum
{
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
    Custom,
}