# LilithsHeart
Core server mod that supports the function of all Lilith Modules and communicates with client(LilithsSoul).

## Dependencies
- BepinEx
- VampireCommandFramework
- LilithsMind (Packaged with LilithsHeart)

## Current Features
- Item Name, Description, and Icon overrides
- Multi language support for overrides (I think it works)
- Consolidated server customization configs from all modules in bepinEx/config/LilithsHeart for convenience
- Reads .json configs from all LilithsHeart/(Category)/(subfolders) to allow server admins to sort their configs
- 3 ways to use prefabs in configs
    - PrefabGUID (862477668)
    - PrefabString (Item_BloodEssence_T01)
    - Alias (BloodEssence)
- Aliases may be renamed to your own



# Configuration

<details>
<summary><strong>LilithsHeart.cfg</strong></summary>

> ### **1) General**
`ServerName = LilithsEngineServer`
- Unique name for this server. 
- Used by Soul clients to cache server-specific configs. 
- **CHANGE THIS** or clients playing on multiple LilithsEngine servers will need to keep redownloading sync.

`DefaultLanguage = English`
- Language used for DisplayName and DescriptionText in your overrides. 
- Soul clients with a different PreferredLanguage will request their language separately. 
- Folder names under Localization/ must match Language names below to support multiple language overrides on your server. 
- English, Brazilian, French, German, Hungarian, Italian, Japanese, Koreana, Latam, Polish, Russian, SChinese, Spanish, TChinese, Thai, Turkish, Ukrainian, Vietnamese, Custom.

> ### **2) Sync**

`ChunksPerFrame = 10`
- Performance setting
- How many chunks of data the server sends per frame when using ChunkPush SyncMode
- Lower this value if there is significant lag on player connect
- Raise at your own risk

`SyncMode = ChunkPush`
- Method Client syncs with server. 
- ChunkPush: 
    - Sync sent as tiered chat chunks on connect
    - May have a performance impact with a lot to sync/many connects at a time 
    - (default, no extra config).
- HttpServer: (**UNTESTED**)
    - Heart hosts an HTTP endpoint; Soul fetches directly
    - Quicker sync download method more performance friendly than ChunkPush
    - (requires HttpPort open in firewall). 
- StaticUrl: (**UNTESTED**)
    - Soul fetches from a Static Url. 
    - Server not responsible for sync so no performance hit
    - Admin must upload SyncCache manually to URL every change. 
    - Best for servers with configs that do not change ofen.

`HttpPort = 7902`
- Port for the HTTP sync endpoint (HttpServer mode only). 
- Must be open in the server firewall. 
- Default: 7902.

`StaticSyncUrl = `
- URL of the hosted sync payload (StaticUrl mode only). 
- e.g. https://example.com/sync.json or a GitHub Gist raw URL. Heart sends this URL to Soul on connect.

`SyncFallbackToChunks = true`
- If HttpServer or StaticUrl mode fails to sync client, Soul requests chunk delivery as a fallback. 
- When false, a failed fetch logs a warning and gives up — the player will not receive server config until they reconnect.

> ### **3) Config Generation**

`GenerateHeartExamples = false`
- Generates Items/ItemExamples.json showing Heart's appearance fields (DisplayName, DescriptionText, Icon). 
- Always overwrites the existing file. 
- Resets to false after generation.

`GenerateAllModuleExamples = false`
- Triggers each module's own example file generation. Always overwrites. 
- Takes priority over GenerateHeartExamples. 
- Resets to false after generation.

`GenerateNameAliasConfigs = false`
- Dumps all PrefabAliases from LilithsMind to Aliases/*.json (one file per index class). Admins can edit these files to use custom aliases in all module configs. Always overwrites. 
- Resets to false after generation.

> ### **4) Debug**

`DebugLogging = false`
- Enable verbose debug logging for LilithsHeart. 
- Useful during development — disable on live servers.

`GenerateDebugConfigs = false`
- Triggers debug config generation for all installed modules. 
- Debug configs have ChangesEnabled=true 
- Used to verify features are working. Always overwrites. 
- Resets to false after 

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
    "DisplayName": "Red Marble",
    "DescriptionText": "A lovely Red Marble dropped from the living, it swirls with life energy.",
    "Icon": "https://raw.githubusercontent.com/AudaciousBovine/LilithsEngine/refs/heads/main/Media/Icons/RedMarble.png"
  }
}

```

> ### Explanations

`"ItemYouAreEditing": { }`
- Accepts PrefabGUID, PrefabString, or PrefabAlias

`"DisplayName":`
- Name you want displayed in UI

`"DescriptionText":`
- Description you want displayed in UI
- Supports colors and values, ill add more information about it later
- Unique to the item type so even items that share descriptions like Swords can have unique descriptions

`"Icon":`
- Icon override, must be a .png file
- Three methods of overriding
    - .png in BepinEx/config/LilithsHeart\Icons
    - URL of a .png
        - It first checks if it has already downloaded the file
        - Downloads to BepinEx/config/LilithsHeart\Icons
    - Use Existing Icon on a vanilla item using its Icon name

</details>

---

<details>
<summary><strong>Prefab Aliases</strong></summary>

> ### Overview

- LilithsHeart/Aliases/*.json
- Aliases are for the convenience of not having to remember codes and strings and so you can name items accurately in your configs
- all aliases DO NOT have to be generated to be used in configs, they all have default values
- all aliases generation is only to be used as reference and deleted before server start or your aliases may not function
- You can have as many alias jsons as you wish named whatever you like
- You can nest jsons within subfolders within Items

> ### Example Config
```
{
  "Item_BloodEssence_T01": "BloodEssence",
  "Item_BloodEssence_T02_Greater": "GreaterBloodEssence",
  "Item_BloodEssence_T03_Primal": "PrimalBloodEssence"
}
```

> ### Explanations

`"ItemYouAreEditing":`
- Accepts only PrefabString (Will add PrefabGUID support)
- Put your Alias after in quotes ""
        - Reccommend not using spaces for clarity
        - Aliases must be unique and cannot be shared across prefabs
        - All Liliths configs accept aliases for in place of PrefabGUID/String

</details>

---

<details>
<summary><strong>Multi Language Support</strong></summary>

> ### Overview
- To support multiple languages create a valid language folder in LilithsHeart/Localization
- Copy your LilithsHeart/Items jsons to LilithsHeart/Localization/(Language)/Items
- Change the DisplayName and DescriptionText to support the language
- You can have as many items jsons as you wish named whatever you like
- You can nest jsons within subfolders within Localization/(Language)/Items
- Valid Language Folder Names
    - English
    - Brazilian
    - French 
    - German 
    - Hungarian
    - Italian
    - Japanese
    - Koreana
    - Latam
    - Polish
    - Russian
    - SChinese
    - Spanish
    - TChinese
    - Thai
    - Turkish
    - Ukrainian
    - Vietnamese
    - Custom

</details>