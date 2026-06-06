# LilithsCookbook
Server module giving admins control over crafting, refining and prisoner feeding

## Dependencies
- BepinEx
- LilithsHeart


## Current Features
- Edit crafting and refining Input Requirements
- Edit crafting and refining Outputs
- Edit crafting and refining craft time
- Edit recipe station availability including player
- Edit if recipe is unlocked from start
- Edit prisoner feed action recipe input requirements
- Edit prisoner feed outputs
- Edit prisoner feed time
- Edit prisoner fed effects (Health, Misery, Blood Quality, Mutate chance)
- Edit Item Stack sizes

# Configuration

<details>
<summary><strong>LilithsCookbook.cfg</strong></summary>

>**1) General**

`ModuleEnabled = true`
- Quick way to disable entire module

>**2) Config Generation**

`GenerateCookbookExamples = false` 
- Generates Cookbook example config files
- Resets itself to false after

>**3) Debug**

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

1. The entire document needs to start and end with { and }
```
{ 
    All the stuff
}
```
2. And sometimes like in the case of Recipes or PrisonerFeeding you need to wrap that in some too!
```
{
    "Recipes": {
        All the Recipes!
    }
}
```
3. Comma ettiquette is important, you have to know when to put a comma and when not to, if you are putting another thing enclosed by { } or [ ] of the same kind you have to put a comma! but when its the last one you **MUST** omit it!

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

<details>
<summary><strong>Recipes/*.json</strong></summary>

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



</details>

<details>
<summary><strong>Items/*.json</strong></summary>




</details>

<details>
<summary><strong>Recipes/PrisonerFeedRecipes.json</strong></summary>




</details>

<details>
<summary><strong>Items/PrisonerFeedItems.json</strong></summary>




</details>