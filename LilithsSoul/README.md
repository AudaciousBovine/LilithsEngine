# LilithsSoul
Core client mod that communicates with the server (LilithsHeart) allowing changes on the server to reflect on the client.

## Dependencies
- BepinEx
- LilithsMind (Packaged with LilithsSoul)

## Synced Features
- Item Name, Description, and Icon overrides
- Unique descriptions for prefabs that share them
- Multi language support for overrides
- Maintained unique sync per server

# Installation
- Download and Install Dependencies
- Download and unzip file
- Place **LilithsSoul.dll** and **LilithsMind.dll** into `(WhereYouInstallGames)/VRising/BepinEx/plugins`
- Done!

# Configuration

<details>
<summary><strong>LilithsSoul.cfg</strong></summary>

> ### **1) Localization**

`PreferredLanguage = System`
- Preferred language for Localization overrides. 
- System (default) automatically detects the language your V Rising client is running in. 
- Set to a specific language to override.
- If the server has not configured the requested language, the server's default language is used instead.
- Languages: **System (Default)**, English, Brazilian, French, German, Hungarian, Italian, Japanese, Koreana, Latam, Polish, Russian, SChinese, Spanish, TChinese, Thai, Turkish, Ukrainian, Vietnamese, Custom

> ### **2) Debug**

`DebugLogging = false` 
- Enable verbose debug logging for LilithsSoul. Useful during development — disable on live servers.

</details>

--- 