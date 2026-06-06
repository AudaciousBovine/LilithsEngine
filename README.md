# LilithsEngine
A modular V Rising mod suite that works together!

# ALPHA TESTING
Gimme Feedback in this discord!  
# https://discord.gg/ejrUvBWcnt

## Downloads
### [LilithsSoul Alpha (Client)](https://github.com/AudaciousBovine/LilithsEngine/releases/tag/LilithsSoul)
### [LilithsHeart Alpha (Server)](https://github.com/AudaciousBovine/LilithsEngine/releases/tag/LilithsHeart)
### [LilithsCookbook Alpha (Server)](https://github.com/AudaciousBovine/LilithsEngine/releases/tag/LilithsCookbook)

## DO NOT USE ON YOUR ONGOING LIVE SERVERS WHEN TESTING
Things can break in early testing so always create new servers or create backups before installing.
## Testing Goals
- Meddle with as many config variations as possible
- With larger influxes of players how is performance, is sync being dropped?
- Do things work differently than expected? (did you read the documentation?)
- Are there unintended effects?
- What happens when you change configs after playing with a different set of configs?
- Do all the configs work?
- Test Multi Language Support
- Test other Sync types
- Test if it plays nice with other mods
- Test if things survive server restarts

## What Invalidates a Bug/Feedback
- Didnt read the documentation
- Player doesn't have LilithsSoul or BepinEx Installed right
- Server doesn't have LilithsHeart or it's dependencies installed right
- Your json config formatting is wrong
- Didn't show me your logs

# The Heart and Soul of the Engine

## LilithsHeart
- Server Side Core
- Houses all config files (For convenience!)
- Facilitates Localization changes
- Communicates neccesary info to LilithsSoul (Client) to make sure UI is in sync
- Registers all installed Modules

## LilithsSoul
- Client Side Core
- Gets communication from LilithsHeart(Server) to make sure UI is in sync
- Caches sync data in a server identity folder to prepatch changes when connecting

# Modules
## LilithsCookbook
- Edit Recipes, Items, Prisoner Food and Effects

# Credits and Special Thanks
> ***deca*** (Discord, VampireCommandFramework and more) - Using VCM for the main Sync method  
> ***Odjit*** (Discord, Kindred Suite) - KindredExtract let me dig in the files  
> ***zlomft*** (Bloodcraft) - Recipes in there were initial inspiration for Cookbook code
> ***Imperivm Draconis*** (Discord) - For being a font of knowledge always explaining things
> ***V Rising Mod Community Discord*** (And everyone in it!)  
> ***Cassapica***, ***Proximo***, ***Lays***, ***Lucas***, ***Ruymber***  
> ***And Everyone Else That Supports My Endeavors and Deal With My Chaos!***  







## Hopeful Additions
### LilithsCookbook
- Add/Edit more crafting stations
- Create Virtual crafting stations
### LilithsBounty
- Edit Drops from Units, Respirces, Chests, More?
### LilithsWisdom
- Edit Unlock Requirements for Recipes, Spells, Passives, Blueprints
### LilithsArchitects
- Edit Blueprint Recipes, Auto Generate Neutral Structures on Plots
### LilithsArmory
- Edit Weapon Stats, Abilities, More?
### LilithsGrimoire
- Edit Spell and Buff Effects, Stats, More?
### LilithsMachinations
- Edit Main Quest, Add Daily, Weekly, Multi Stage Quests, Dialogue Boxes
### LilithsMenagerie
- Variant Horse Stats, Breeding and Training Horses, Spiders, Rats, Ravens.
### LilithsAdversaries
- Edit Units, V Bloods, Custom Spawns, Another Wanted System?
### LilithsNexus
- Teleportation Variants, Personal, Clan, Temporary, Permanent, Cooldowns, Teleport Cost
### LilithsConquest
- Edit Servant Missions, New Mission Layer, Train Units, PvP Layer?
### LilithsRituals
- Set up Individual, Clan, or Server wide Blessing and Curses that can be temporary or permanent
### LilithsTreasury
- Virtual Storages, Personal, Clan, Multiple
- Use units from Conquest or Menagerie to send items
### And More! (Secret)

<details>
<summary><strong>Secrets</strong></summary>

*Italic*  
**Bold**  
***BoldItalic***  
`code`  
```
Code Block
```
- Bullet
    - Sub Bullet
        - Sub Sub Bullet

# Massive Text
## Huge Text
### Large Text
#### Normal Text
##### Small Text
###### Tiny Text

> Quote

> Multi Line
>   > Nested Quote
>   >   > Double Nested Quote
>   >   >   > More Nesting
>   >   >   >   > Stairs
>   >   >   >   >   > It keeps going

| Table | Table |
| ---- | ---- |
| Data | Data |
| Data | Data | 

Above the line
---
Under the line

- [x] Done thing
- [ ] Not Done Thing

</details>
