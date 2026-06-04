// ============================================================
//  ServerSyncPayload — LilithsMind
//  LilithsMind/Network/ServerSyncPayload.cs
//
//  The data contract sent from LilithsHeart to LilithsSoul on
//  client connect. Shared between both projects via LilithsMind
//  as the single source of truth.
//
//  [CHANGED] ServerLanguage added — the language code used to
//            populate ItemAppearanceOverrides (DisplayName +
//            DescriptionText) on this server. Soul compares this
//            against its PreferredLanguage setting. If they differ
//            and the server has the requested language configured,
//            a separate LocalizationSyncPayload follows.
//
//  [PERFORMANCE] Plain DTO — no ECS types, no Unity dependencies.
//                Serialized once on connect by Heart, deserialized
//                once on receipt by Soul.
// ============================================================

using LilithsMind.Data;

namespace LilithsMind.Network;

/// <summary>
/// The full data bundle Heart sends to a connecting Soul client.
/// Shared contract — do not add Heart-only or Soul-only logic here.
/// </summary>
public sealed class ServerSyncPayload
{
    // ── Identity ────────────────────────────────────────────

    /// <summary>
    /// Server name, sanitized for use as a folder name.
    /// Soul uses this to scope its disk cache per server.
    /// </summary>
    public string ServerIdentity { get; set; } = string.Empty;

    /// <summary>
    /// Short SHA256 hash of the serialized payload.
    /// Soul compares this against its cached sync.json hash
    /// to skip redundant disk writes and re-injection on reconnect.
    /// </summary>
    public string PayloadHash { get; set; } = string.Empty;

    /// <summary>
    /// The language used for DisplayName and DescriptionText in
    /// ItemAppearanceOverrides. Soul compares this against its
    /// PreferredLanguage setting to decide whether to request a
    /// localization payload.
    ///
    /// [CHANGED] Added for multi-language localization support.
    /// Matches LanguageCodeEnum name (e.g. "English", "Spanish").
    /// Defaults to "English".
    /// </summary>
    public string ServerLanguage { get; set; } = "English";

    // ── Item appearance overrides ────────────────────────────

    /// <summary>
    /// Item appearance overrides keyed by prefab name.
    /// DisplayName and DescriptionText are in the server's default
    /// language (ServerLanguage). If Soul's PreferredLanguage differs,
    /// it requests a LocalizationSyncPayload to override these.
    /// Icon overrides are language-independent and always applied.
    /// </summary>
    public Dictionary<string, LilithItemData> ItemAppearanceOverrides { get; set; } = new();

    // ── Recipe overrides ─────────────────────────────────────

    /// <summary>
    /// Recipe data overrides keyed by recipe prefab name.
    /// </summary>
    public Dictionary<string, LilithRecipeData> RecipeOverrides { get; set; } = new();

    /// <summary>
    /// Station recipe overrides keyed by station prefab name.
    /// </summary>
    public Dictionary<string, LilithStationData> StationRecipeOverrides { get; set; } = new();

    // ── Player crafting overrides ────────────────────────────

    /// <summary>
    /// Recipe prefab names to add to the client player's recipe list.
    /// </summary>
    public List<string> PlayerRecipesToAdd { get; set; } = new();

    /// <summary>
    /// Recipe prefab names to remove from the client player's recipe list.
    /// </summary>
    public List<string> PlayerRecipesToRemove { get; set; } = new();
}