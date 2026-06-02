// ============================================================
//  PrefabDef — LilithsMind
//  LilithsMind/Prefabs/PrefabDef.cs
//
//  Universal prefab definition record, shared between
//  LilithsHeart (server) and LilithsSoul (client).
//
//  Design decisions:
//  ─────────────────
//  • Lives in LilithsMind so both Heart and Soul reference
//    an identical catalogue without duplicating definitions.
//    This is the single source of truth for all prefab metadata.
//
//  • GuidHash is a plain int rather than PrefabGUID. PrefabGUID
//    is just a wrapper around a single int (_Value) — storing the
//    int directly keeps LilithsMind free of any V Rising assembly
//    dependencies. Heart and Soul construct new PrefabGUID(def.GuidHash)
//    at the point of ECS use. Cost: one struct construction — negligible.
//
//  • Name, NameKey, and DescKey are nullable (string?) to represent
//    genuine incompleteness. An empty string would mask whether a
//    field is "not yet filled" vs "intentionally blank". Nulls make
//    partial entries self-documenting and allow consuming code to
//    guard cleanly: if (def.NameKey is not null) { ... }
//
//  • GuidHash and Prefab are non-nullable — every entry must have
//    these. They are the two fields always derivable from game data.
//
//  • readonly record struct gives value semantics and a free
//    Equals / GetHashCode / ToString. Stack-allocated — no heap
//    pressure from holding thousands of entries in static fields.
//
//  Population states:
//  ──────────────────
//  Stub     — GuidHash + Prefab only.
//  Partial  — GuidHash + Prefab + Name.
//  Complete — All five fields filled.
//
//  [CHANGED] NameKey is NO LONGER REQUIRED for display-name overrides.
//            LocalizationSoul-side, LocalizationPatcher repoints names by
//            resolving the item via PrefabGUID, reading its live
//            ManagedItemData.Name, and pointing it at a freshly minted
//            AssetGuid — it never reads the recorded NameKey. A Stub
//            (GuidHash + Prefab) is enough to rename an item. NameKey and
//            DescKey are retained as a vanilla-key reference record and for
//            the pending tooltip path (a Harmony patch that may need
//            DescKey). Do not bulk-delete them: bulk find-and-replace over
//            definition files risks corrupting ECS buffer type names.
//
//  [PERFORMANCE] readonly record struct is stack-allocated.
//                Holding thousands of these as static readonly
//                fields in definition classes carries zero heap
//                cost. Field access is a direct memory read — O(1).
// ============================================================

namespace LilithsMind.Prefabs;

/// <summary>
/// Universal definition record for a V Rising prefab.
/// Shared between LilithsHeart and LilithsSoul via LilithsMind.
/// Covers all categories — items, spells, units, stations, and more.
/// </summary>
public readonly record struct PrefabDef
{
    /// <summary>
    /// Human-readable name for this prefab.
    /// Used in config files, logging, and admin commands.
    /// Null until manually filled.
    /// </summary>
    public string?  Name     { get; init; }

    /// <summary>
    /// The raw integer hash that identifies this prefab in the ECS.
    /// This is the value of PrefabGUID._Value.
    /// Heart and Soul construct new PrefabGUID(def.GuidHash) at the
    /// point of ECS use — one struct construction, negligible cost.
    /// </summary>
    public int      GuidHash { get; init; }

    /// <summary>
    /// The internal asset name as defined by Stunlock.
    /// e.g. "Item_Weapon_Sword_T01_Bone"
    /// Always known. Useful for debugging, cross-referencing,
    /// and as the key in admin config files.
    /// </summary>
    public string   Prefab   { get; init; }

    /// <summary>
    /// Vanilla localization key for the display name shown in-game.
    /// OPTIONAL and reference-only: LocalizationPatcher does NOT read this
    /// to override names — it mints a fresh key and repoints the live
    /// ManagedItemData.Name (resolved by PrefabGUID). Retained as a record
    /// of the item's original name GUID. Null until manually filled.
    /// </summary>
    public string?  NameKey  { get; init; }

    /// <summary>
    /// Vanilla localization key for the item description/tooltip shown in-game.
    /// Reference data; the (pending) tooltip Harmony patch may consume it.
    /// Not used by the current name-override path. Null until manually filled.
    /// </summary>
    public string?  DescKey  { get; init; }
}