# Prefab Definition System Reference

## Overview

The prefab catalog is a **compile-time, type-safe approach** to defining V Rising game entities. Instead of runtime JSON/XML parsing, all prefabs are defined as `static readonly PrefabDef` fields in static classes under `LilithsMind/Prefabs/Definitions/`. These are discovered at runtime via **reflection** by both Heart and Soul.

## PrefabDef Record

**File:** `LilithsMind/Prefabs/PrefabDef.cs`

```csharp
public readonly record struct PrefabDef
{
    string?  Name;     // Human-readable admin name (e.g. "BoneSword")
    int      GuidHash; // Raw int from PrefabGUID._Value
    string   Prefab;   // Game asset name (e.g. "Item_Weapon_Sword_T01_Bone")
    string?  NameKey;  // Vanilla localization AssetGuid for display name (reference only — see note)
    string?  DescKey;  // Vanilla localization AssetGuid for tooltip (reference only — see note)
}
```

> **`NameKey` and `DescKey` are no longer required for ANY appearance override.**
> Both the display-name path (`LocalizationPatcher`) and the description path
> (`DescriptionPatcher`) **repoint**: they resolve an item by `PrefabGUID`, mint
> a fresh `AssetGuid`, write the new string to `Localization._LocalizedStrings`,
> and point the item's live value-type localization key at it
> (`ManagedItemData.Name`, or the `Key` field of the `Description` struct).
> Neither path consults the recorded `NameKey`/`DescKey`. An item is fully
> renamable AND re-describable with only a **Stub** definition
> (GuidHash + Prefab). The `NameKey`/`DescKey` fields are retained solely as a
> record of each item's vanilla localization GUIDs. Do not bulk-delete them —
> see "Important Constraints".

## Population States

| State | Fields Filled | Use Case |
|-------|---------------|----------|
| **Stub** | GuidHash + Prefab | Minimum viable. **Sufficient for name AND description overrides** (both patchers mint their own keys). |
| **Partial** | + Name | Admin-friendly config keys, logging, command output |
| **Complete** | + NameKey + DescKey | Vanilla-key reference record only. **No longer required by any appearance path.** |

## Discovery Pattern

Both Heart and Soul reflectively scan `typeof(PrefabDef).Assembly` for static classes in namespace `LilithsMind.Prefabs.Definitions`:

```csharp
var types = typeof(PrefabDef).Assembly.GetTypes()
    .Where(t => t.IsClass && t.IsAbstract && t.IsSealed &&
                t.Namespace == "LilithsMind.Prefabs.Definitions");

foreach (var type in types) {
    var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(PrefabDef));
    foreach (var field in fields) {
        var def = (PrefabDef)field.GetValue(null)!;
        // ... build lookup dictionaries
    }
}
```

## Server-Side Lookups (PrefabNameResolver)

| Dictionary | Key | Value | Source |
|------------|-----|-------|--------|
| `_nameToGuid` | `def.Name` | `PrefabGUID` | LilithsMind Name field |
| `_prefabToGuid` | `def.Prefab` | `PrefabGUID` | LilithsMind Prefab field |
| `_guidToName` | `def.GuidHash` | `string` | Prefers Name, falls back to Prefab |

## Client-Side Lookups

### RecipePatcher._nameToGuid (Two Sources)
1. `PrefabCollectionSystem._PrefabDataLookup.AssetName` → GUID (ECS source of truth)
2. LilithsMind definition `Name` fields → same GUID (admin alias support)

### LocalizationPatcher._nameToPrefabGuid (display-name repoint)
| Dictionary | Key | Value |
|------------|-----|-------|
| `_nameToPrefabGuid` | Prefab string or Name | `PrefabGUID` |
| `_previousNames` | `GuidHash` (int) | original `LocalizationKey`, captured for `ClearPrevious()` restore |

### DescriptionPatcher._descToPrefabGuid (description repoint)
| Dictionary | Key | Value |
|------------|-----|-------|
| `_descToPrefabGuid` | Prefab string or Name | `PrefabGUID` |
| `_previousDescriptions` | `GuidHash` (int) | original `LocalizedStringBuilderBase` struct, captured for `Clear()` restore |

> Both patchers map name → **PrefabGUID** (to locate the item), not name →
> AssetGuid. They read the item's current key off `ManagedItemData` at apply
> time and mint a replacement, so they need no recorded localization GUID.

## Definition File Naming Convention

Each file uses a `public static class` (or `partial class`) named with the category + `Index` suffix:

| File | Class | Lines | Exhaustiveness |
|------|-------|-------|----------------|
| `WeaponsIndex.cs` | `WeaponsIndex` | ~1453 | All weapons |
| `AccessoryIndex.cs` | `AccessoryIndex` | ~274 | All filled out with NameKey/DescKey |
| `StationsIndex.cs` | `StationsIndex` | — | All stations |
| `BagIndex.cs` | `BagIndex` | — | All bags |
| `SaddleIndex.cs` | `SaddleIndex` | — | All saddles |
| `ArmorHeadIndex.cs` | `ArmorHeadIndex` | — | All head armor |
| `ArmorChestIndex.cs` | `ArmorChestIndex` | — | All chest armor |
| `ArmorLegsIndex.cs` | `ArmorLegsIndex` | — | All leg armor |
| `ArmorGlovesIndex.cs` | `ArmorGlovesIndex` | — | All gloves |
| `ArmorBootsIndex.cs` | `ArmorBootsIndex` | — | All boots |
| `ArmorCloakIndex.cs` | `ArmorCloakIndex` | — | All cloaks |
| `ItemsResourcesIndex.cs` | `ItemsResourcesIndex` | — | All resources |
| `ItemsUsableIndex.cs` | `ItemsUsableIndex` | — | All usable items |
| `ItemsMiscIndex.cs` | `ItemsMiscIndex` | — | All misc items |
| `ItemsJewelIndex.cs` | `ItemsJewelIndex` | — | All jewels |
| `ItemsBookIndex.cs` | `ItemsBookIndex` | — | All books |
| `RecipesWeaponIndex.cs` | `RecipesWeaponIndex` | — | All weapon recipes |
| `RecipesUseableIndex.cs` | `RecipesUseableIndex` | — | All usable recipes |
| `RecipesResourceIndex.cs` | `RecipesResourceIndex` | — | All resource recipes |
| `RecipesMiscIndex.cs` | `RecipesMiscIndex` | — | All misc recipes |
| `RecipesJewelIndex.cs` | `RecipesJewelIndex` | — | All jewel recipes |
| `RecipesEquipmentIndex.cs` | `RecipesEquipmentIndex` | — | All equipment recipes |

## PrefabDef.Name Convention (no spaces)

The `Name` field uses no spaces, matching the `[PrefabName]` attribute style
(e.g. `"BanditBag"`, not `"Bandit Bag"`). Fields are named after the prefab
string exactly (e.g. `Item_NewBag_T01`) — no invented tier or category names.

## Example Entry (Complete — AccessoryIndex)

```csharp
public static readonly PrefabDef Item_MagicSource_BloodKey_T01 = new()
{
    Name    = "BloodKey",
    GuidHash = 1655869633,
    Prefab  = "Item_MagicSource_BloodKey_T01",
    NameKey = "4e77a4af-348e-41d0-88ae-ecbe993d3fa6",
    DescKey = "6d953637-aa08-41db-8c8e-4404483d66d7",
};
```

## Example Entry (Stub — WeaponsIndex)

```csharp
public static readonly PrefabDef Item_Weapon_Sword_T02_Bone_Reinforced = new()
{
    Name    = "ReinforcedBoneSword",
    GuidHash = -796306296,
    Prefab  = "Item_Weapon_Sword_T02_Bone_Reinforced",
    NameKey = null,
    DescKey = null,
};
```

> A Stub like this is **fully renamable AND re-describable**: with
> `NameKey`/`DescKey` null, both patchers mint fresh keys and repoint
> regardless. (The retired injector would have silently skipped a null key.)

## Adding a New Prefab Entry

1. Determine the category (weapon, armor, item, recipe, etc.)
2. Open the appropriate `*Index.cs` file in `LilithsMind/Prefabs/Definitions/`
3. Add a `public static readonly PrefabDef` field with the prefab's constant name matching the game's Prefab string naming convention
4. Fill GuidHash and Prefab (required)
5. Add Name if you want admin-friendly config keys
6. `NameKey`/`DescKey` are **optional reference data** — not needed for name or description overrides. Add them only as a vanilla-key record (find GUIDs in `LilithsMind/Resources/English.json`).

## Important Constraints

- `GuidHash` is a **signed int** — can be negative. It maps to `PrefabGUID._Value`.
- `NameKey` and `DescKey` are **string GUIDs** (not integers) — they map to `AssetGuid` in `Localization._LocalizedStrings`.
- **No appearance override requires `NameKey` or `DescKey`.** Items with these
  null are renamed and re-described normally (both patchers mint their own
  keys). Do **not** mass-delete `NameKey`/`DescKey` from the indexes: they are
  reference data, and bulk find-and-replace across definition files risks
  corrupting ECS buffer type names (e.g. `RecipeRequirementBuffer`,
  `RecipeOutputBuffer`) — verify ECS types after any rename (see CONVENTIONS).
- An entry's `Name` field takes priority over `Prefab` in all forward lookups — config files can use either form.
- `PrefabDef` is a `readonly record struct` — value semantics, no heap allocation.

## Known Data Issue

`StationsIndex.CraftingStation_Player` has a **null `Prefab`** field — it logs a
skip warning in both `LocalizationPatcher.BuildMap()` and
`DescriptionPatcher.BuildMap()` at world ready ("Definition
StationsIndex.CraftingStation_Player has null Prefab — skipped"). Harmless to
the appearance pipeline, but the entry should be fixed (supply the correct
prefab asset name) to clear the warning.