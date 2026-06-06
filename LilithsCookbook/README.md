# LilithsCookbook
Server module giving admins control over crafting, refining and prisoner feeding

## Dependencies
- BepinEx
- LilithsHeart
- LilithsMind (Packaged with LilithsHeart)


## Current Features
- Edit crafting and refining recipes
- Edit recipe station availability including player
- Edit prisoner feed action recipes and effects
- Edit Item Stack sizes

# Installation
- Download and Install Dependencies
- Download and unzip file
- Place **LilithsCookbook.dll** into `(WhereYouInstallGames)/VRising/BepinEx/plugins`
- Done!

# Configuration

<details>
<summary><strong>LilithsCookbook.cfg</strong></summary>

> ### **1) General**

`ModuleEnabled = true`
- Quick way to disable entire module

> ### **2) Config Generation**

`GenerateCookbookExamples = false` 
- Generates Cookbook example config files
- Resets itself to false after

> ### **3) Debug**

`GenerateAllRecipes = false`
- Generates a file with all vanilla recipes in Recipes/AllRecipes.json with 
`ChangesEnabled=false`.
- Use as a reference when making recipe changes but remove from Recipes when starting server or it may disable your changes.
- Resets itself to false after

`GenerateCookbookDebugConfigs = false`
- Generates Cookbook debug config files, these are just to test if features are working
- Resets itself to false after
</details>

--- 

<details>
<summary><strong>Understanding The Json Configs</strong></summary>

### 1. The entire document needs to start and end with { and }
```
{ 
    All the stuff
}
```
### 2. And sometimes like in the case of Recipes or PrisonerFeeding you need to wrap that in some too!
```
{
    "Recipes": {
        All the Recipes!
    }
}
```
### 3. Comma ettiquette is important, you have to know when to put a comma and when not to, if you are putting another thing enclosed by { } or [ ] of the same kind you have to put a comma! but when its the last one you **MUST** omit it!

```
  "Recipes": {
    "RecipeOfSomeKind": {
      "ChangesEnabled": true, <----- Commas cause theyre more attributes within
      "CraftDuration": 1.0,             "RecipeOfSomeKind"
      "Requirements": [
        { "Item": "Bone", "Amount": 1 }, <--- Comma cause more "Requirements"
        { "Item": "Wood", "Amount": 1 } <--- No Comma its the last Requirement
      ], <--- Comma that closes Requirements attribute and move to Outputs attribute
      "Outputs": [
        { "Item": "BoneSword", "Amount": 1 } <--- No comma, just one output
      ],
      "Stations": [ "PlayerCrafting", "SimpleWorkbench" ] <--- Comma within
    }
  }
```

</details>

---

<details>
<summary><strong>Recipes/*.json</strong></summary>

> ### Overview

- Recipe files should be in BepinEx/config/LilithsHeart/Recipes
- You can have as many recipe jsons as you wish named whatever you like
- You can nest jsons within subfolders within Recipes

> ### Example Config

```
{
  "_readme": "You can write anything here",

  "Recipes": {
    "RecipeBoneSword": {
      "ChangesEnabled": true,
      "AlwaysUnlocked": true,
      "CraftDuration": 2.0,
      "Requirements": [
        { "Item": "Bone", "Amount": 1 },
        { "Item": "Wood", "Amount": 1 }
      ],
      "Outputs": [
        { "Item": "BoneSword", "Amount": 1 }
      ],
      "Stations": [ "PlayerCrafting", "SimpleWorkbench" ]
    }
  }
}
```
> ### Explanations

`"Recipes": { }`
- All recipes need to be within this wrapper

`"RecipeYouAreChanging" { }`
- Accepts PrefabGUID, PrefabString, or PrefabAlias
- If multiple jsons have the same recipe entry, they override alphabetically
- You can however alter different attributes of the same recipe in different files, ex: one file has outputs, one has inputs

`"ChangesEnabled": true`
- Set to `false` to quickly revert recipe to vanilla (Requires Restart)

`"AlwaysUnlocked": true`
- **NOT SURE IF WORKS**, should make recipe not require unlocking if `true`

`"CraftDuration": 0.0`
- Set duration to craft or refine in seconds

`"Requirements": [ ]`
- Set Items and Amounts required to start craft or refinement
- Accepts PrefabGUID, PrefabString, or PrefabAlias

`"Outputs": [ ]`
- Set Items and Amounts gained after craft or refinement is complete
- Accepts PrefabGUID, PrefabString, or PrefabAlias

`"Stations": [ ]`
- Accepts PrefabGUID, PrefabString, or PrefabAlias
- List of stations this recipe appears in
- Stations can be of Crafting or Refinement type, both function together
- Any stations not present will have the recipe removed if it is there vanilla

</details>

<details>
<summary><strong>Items/*.json</strong></summary>

> ### Overview

- Item files should be in BepinEx/config/LilithsHeart/Items
- You can add Editable Attributes added by Modules to existing configs
- You can have as many item jsons as you wish named whatever you like
- You can nest jsons within subfolders within Items

> ### Example Config

```
{
  "BloodEssence": {
    "ChangesEnabled": false,
    "StackSize": 1000
  }
}
```

> ### Explanations

`"ItemYouAreEditing": { }`
- Accepts PrefabGUID, PrefabString, or PrefabAlias

`"ChangesEnabled": true`
- Set to `false` to quickly revert recipe to vanilla (Requires Restart)

`"StackSize": 4095`
- Set the Maximum amount in a stack, values above 4095 do not display in UI (But are still there)

</details>

<details>
<summary><strong>Recipes/PrisonerFeedRecipes.json</strong></summary>

> ### Overview

> ### Example Config

```
{
  "Recipes": {
    "RecipeFeedRat": {
      "ChangesEnabled": false,
      "CraftDuration": 10.0,
      "Requirements": [
        { "Item": "Rat", "Amount": 1 }
      ],
      "Outputs": [
        { "Item": "FeedRat", "Amount": 1 }
      ]
    },

    "RecipeExtractBloodEssence": {
      "ChangesEnabled": false,
      "CraftDuration": 5.0,
      "Outputs": [
        { "Item": "ExtractBloodEssence", "Amount": 1 },
        { "Item": "BloodEssence", "Amount": 15 }
      ]
    }
  }
}
```

> ### Explanations

`"Recipes": { }`
- All recipes need to be within this wrapper
- Prisoner Actions count as recipes

`"PrisonerFeedActionRecipe" { }`
- Accepts PrefabGUID, PrefabString, or PrefabAlias

`"ChangesEnabled": true`
- Set to `false` to quickly revert recipe to vanilla (Requires Restart)

`"CraftDuration": 0.0`
- Set duration it takes to complete prisoner action in seconds

`"Requirements": [ ]`
- Set Items and Amounts required for prisoner action
- Accepts PrefabGUID, PrefabString, or PrefabAlias

`"Outputs": [ ]`
- Set Items and Amounts gained after prisoner action is completed
- **Must have a PrisonerFeedItem to have effect on prisoner stats**
- Accepts PrefabGUID, PrefabString, or PrefabAlias

</details>

<details>
<summary><strong>Items/PrisonerFeedItems.json</strong></summary>

> ### Overview

> ### Example Config

```
{
  "PrisonerFeeding": {
    "FeedRat": {
      "ChangesEnabled": false,
      "Type": "FeedPrisoner",
      "RecoverHealth_Min": 0.05,
      "RecoverHealth_Max": 0.15,
      "RecoverMisery_Min": 0.01,
      "RecoverMisery_Max": 0.02
    },

    "ExtractBloodEssence": {
      "ChangesEnabled": false,
      "Type": "DealDamageToPrisoner",
      "DealPercentualDamage_Min": 0.05,
      "DealPercentualDamage_Max": 0.25,
      "DealPercentualTorture_Min": 0.10,
      "DealPercentualTorture_Max": 0.20
    },

    "FeedIrradiantGruel": {
      "ChangesEnabled": false,
      "Type": "AffectWithToxic",
      "ChanceToBecomeMutant": 0.95,
      "IncreaseBloodQuality_Min": 0.50,
      "IncreaseBloodQuality_Max": 0.99
    }
  }
}
```

> ### Explanations

`"PrisonerFeeding": { }`
- All PrisonerActionItems need to be within this wrapper

`"PrisonerFeedItem": { }`
- Item that holds the prisoner effects

`"ChangesEnabled": true`
- Set to `false` to quickly revert recipe to vanilla (Requires Restart)

`"Type": FeedPrisoner`
- One of three types that dictates effects available
- At this time types **CANNOT** be changed

`"RecoverHealth_Min/Max": 0.0`
- Range in % Prisoner is healed (*1.0 = 100%, 0.01 = 1%*)

`"RecoverMisery_Min/Max": 0.0`
- Range in % Prisoner Misery is reduced by (*1.0 = 100%, 0.01 = 1%*)

`"DealPercentualDamage_Min/Max": 0.0`
- Range in % Prisoner is damaged (*1.0 = 100%, 0.01 = 1%*)

`"DealPercentualTorture_Min/Max": 0.0`
- Range in % Prisoner Misery is increased by (*1.0 = 100%, 0.01 = 1%*)

`"ChanceToBecomeMutant": 0.0`
- Range in % chance prisoner will transform into a mutant abomination (*1.0 = 100%, 0.01 = 1%*)

`"IncreaseBloodQuality_Min/Max": 0.0`
- Range in % Prisoner Blood Quality will increase by (*1.0 = 100%, 0.01 = 1%*)

</details>