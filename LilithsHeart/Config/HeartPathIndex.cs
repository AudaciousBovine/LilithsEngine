// ============================================================
//  HeartPathIndex — LilithsHeart
//  LilithsHeart/Config/HeartPathIndex.cs
//
//  Single source of truth for every filesystem path used by
//  LilithsHeart and its child modules.
//
//  All config for the entire suite lives under one root:
//      BepInEx/config/LilithsHeart/
//
//  Structure:
//      LilithsHeart/
//          LilithsHeart.cfg            ← Heart core settings
//          LilithsCookbook.cfg         ← child module cfg files
//          Items/                      ← item overrides (appearance + functional)
//              example.json
//              Currencies/
//              Weapons/
//          Recipes/                    ← recipe config (LilithsCookbook)
//          MainQuest/                  ← quest text (LilithsMachinations, future)
//          Spells/                     ← spell names/tooltips (LilithsGrimoire, future)
//
//  [CHANGED] ItemsDir comment updated — Items/ now holds all item
//            override fields (DisplayName, DescriptionText, Icon,
//            StackSize) parsed in one pass by LocalizationService
//            into LilithItemConfig's two dictionaries.
//
//  Child modules register their own directories with
//  LocalizationService.RegisterDirectory() rather than adding
//  named paths here — HeartPathIndex only needs to know about
//  directories that Heart core itself owns.
// ============================================================

namespace LilithsHeart.Config;

public static class HeartPathIndex
{
    // ── Root ────────────────────────────────────────────────

    /// <summary>
    /// BepInEx/config/LilithsHeart/
    /// All suite config lives under this directory.
    /// </summary>
    public static readonly string Root = Path.Combine(
        BepInEx.Paths.ConfigPath,
        "LilithsHeart"
    );

    // ── .cfg files ──────────────────────────────────────────

    /// <summary>
    /// BepInEx/config/LilithsHeart/LilithsHeart.cfg
    /// The core Heart config file. Pass to HeartConfig.Initialize().
    /// </summary>
    public static readonly string CoreConfig = Path.Combine(Root, "LilithsHeart.cfg");

    /// <summary>
    /// Returns the path for a child module's .cfg file.
    /// e.g. HeartPathIndex.ModuleConfig("LilithsCookbook")
    ///      → BepInEx/config/LilithsHeart/LilithsCookbook.cfg
    ///
    /// Pass the result directly to: new ConfigFile(path, saveOnInit: true)
    /// </summary>
    public static string ModuleConfig(string moduleName)
        => Path.Combine(Root, $"{moduleName}.cfg");

    // ── Data subdirectories ─────────────────────────────────

    /// <summary>
    /// BepInEx/config/LilithsHeart/Items/
    /// All item override files — appearance (DisplayName, DescriptionText,
    /// Icon) and functional (StackSize). Parsed in one pass by
    /// LocalizationService into LilithItemConfig's two dictionaries.
    ///
    /// Scanned recursively — admins can create subdirectories freely
    /// (e.g. Items/Currencies/, Items/Weapons/).
    /// Registered with LocalizationService by Heart.OnInitialize().
    /// </summary>
    public static readonly string ItemsDir = Path.Combine(Root, "Items");

    /// <summary>
    /// Returns the path for a named data subdirectory.
    /// e.g. HeartPathIndex.DataDir("Recipes")
    ///      → BepInEx/config/LilithsHeart/Recipes/
    ///
    /// The directory is NOT created here — call Directory.CreateDirectory()
    /// at the point of first use so empty folders are not left on disk
    /// for features that are never exercised.
    /// </summary>
    public static string DataDir(string category)
        => Path.Combine(Root, category);
}