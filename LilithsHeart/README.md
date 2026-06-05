# LilithsHeart
Core server mod that supports the function of all Lilith Modules and communicates with client(LilithsSoul).

## Dependencies
- BepinEx
- VampireCommandFramework
- LilithsMind

## Current Features
- Item Name, Description, and Icon overrides
- Multi language support for overrides
- Consolidated server customization configs from all modules in bepinEx/config/LilithsHeart for convenience
- Reads .json configs from all LilithsHeart/(Category)/(subfolders) to allow server admins to sort their configs
- 3 ways to use prefabs in configs
 - PrefabGUID (862477668)
 - PrefabString (Item_BloodEssence_T01)
 - Alias (BloodEssence)
- Aliases may be renamed to your own

## Configuration
- Main configuration bepinEx/config/LilithsHeart.cfg
- Set unique server name that dictates the directory sync is stored in
- Set server default language, used to match what language players are looking for with multi language overrides
- Set one of three sync methods, Server chunks, Server hosted, External Url
- Set if failure fallbacks to chunks
- Set chunks per frame for performance
- Generate example override configs
- Generate all module example configs
- Enable Debug
- Generate Debug override configs
- Generate all module Debug configs