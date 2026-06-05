// ============================================================
//  ColorTranslator — LilithsSoul
//  LilithsSoul/Services/ColorTranslator.cs
//
//  Translates V Rising's named colour tags to Unity rich text
//  markup before strings are injected into _LocalizedStrings.
//
//  Why this exists:
//  ─────────────────
//  V Rising's tooltip pipeline processes named colour tags
//  (e.g. <teal1>, </c>) at a layer above our injection point.
//  Strings injected directly into Localization._LocalizedStrings
//  bypass that layer, so named tags appear as literal text.
//  Unity rich text (<color=#...>...</color>) IS processed at
//  the render layer and works correctly in injected strings.
//
//  This translator runs a single pass over any injected string,
//  replacing all known V Rising tags with their Unity equivalents.
//  The tag map is sourced directly from V Rising's localization
//  colour code definitions.
//
//  Usage:
//  ───────
//  Called from LocalizationPatcher and DescriptionPatcher
//  immediately before writing to _LocalizedStrings:
//      table[g] = ColorTranslator.Translate(text);
//
//  Unknown tags are left as-is — they may be structural tags
//  handled elsewhere, or typos in admin config. No exception is
//  thrown; the string is returned with unknown tags intact.
//
//  [PERFORMANCE] Tag map is a static readonly dictionary — built
//                once at class init, O(1) per tag lookup.
//                Translate() iterates the map once per string —
//                O(tags × string length) at inject time, which
//                is once per connect. Zero per-frame cost.
// ============================================================

namespace LilithsSoul.Services;

public static class ColorTranslator
{
    // Sourced directly from V Rising's localization colour code definitions.
    // Ordered: closing tag first so </c> is replaced before any opening tag
    // processing could interfere. Opening tags are sorted longest-first to
    // avoid partial matches (e.g. <teal1> before a hypothetical <teal>).
    static readonly (string VRising, string Unity)[] _tags =
    [
        // ── Closing tag ──────────────────────────────────────
        ( "</c>",          "</color>"            ),

        // ── Teal family ──────────────────────────────────────
        ( "<teal4>",       "<color=#649AA6>"     ),
        ( "<teal3>",       "<color=#B2F2FF>"     ),
        ( "<teal2>",       "<color=#6B9EB2>"     ),
        ( "<teal1>",       "<color=#82C9D9>"     ),

        // ── Red family ───────────────────────────────────────
        ( "<red3>",        "<color=#961D33>"     ),
        ( "<red2>",        "<color=#DD1514>"     ),
        ( "<red1>",        "<color=#C52443>"     ),

        // ── Yellow family ────────────────────────────────────
        ( "<yellow2>",     "<color=#F4EE58>"     ),
        ( "<yellow1>",     "<color=#D9C882>"     ),

        // ── Team colours ─────────────────────────────────────
        ( "<teamcolor04>", "<color=#A84DFF>"     ),
        ( "<teamcolor03>", "<color=#33FF33>"     ),
        ( "<teamcolor02>", "<color=#33A8FF>"     ),
        ( "<teamcolor01>", "<color=#FF3333>"     ),

        // ── Named single colours ─────────────────────────────
        ( "<statscolor>",  "<color=#8A9499>"     ),
        ( "<skillcolor>",  "<color=#E6E6E6>"     ),
        ( "<legendary>",   "<color=#ff8500>"     ),
        ( "<illusion>",    "<color=#37dfb9>"     ),
        ( "<unholy>",      "<color=#4ddb00>"     ),
        ( "<chaos>",       "<color=#e25dff>"     ),
        ( "<storm>",       "<color=#fdff5d>"     ),
        ( "<green1>",      "<color=#5DC014>"     ),
        ( "<gray1>",       "<color=#909090>"     ),
        ( "<white>",       "<color=white>"       ),
        ( "<blood>",       "<color=#C52443>"     ),
        ( "<frost>",       "<color=#03a3ff>"     ),
        ( "<epic>",        "<color=#ff00ff>"     ),
        ( "<rare>",        "<color=#008aff>"     ),
    ];

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Replaces all known V Rising colour tags in the given string
    /// with their Unity rich text equivalents.
    /// Unknown tags are left as-is.
    /// Returns the input string unchanged if it contains no known tags.
    ///
    /// [PERFORMANCE] O(tags × string length) — called once per
    ///               injected string at connect time. Zero steady-state cost.
    /// </summary>
    public static string Translate(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Fast-path: skip the replacement loop entirely if the string
        // contains no tag-like content. '<' is a reliable sentinel —
        // all V Rising tags start with it.
        if (!text.Contains('<')) return text;

        foreach (var (vRising, unity) in _tags)
            text = text.Replace(vRising, unity, StringComparison.Ordinal);

        return text;
    }
}