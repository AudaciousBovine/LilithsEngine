namespace LilithsHeart.Config;

public static class HeartPathIndex
{
/// BepInEx/config/LilithsHeart/
/// All suite config lives under this directory.
    public static readonly string Root = Path.Combine(
        BepInEx.Paths.ConfigPath,
        "LilithsHeart"
    );

    // ── .cfg files ──────────────────────────────────────────

    /// <summary>
    /// BepInEx/config/LilithsHeart/LilithsHeart.cfg
    /// </summary>
    public static readonly string CoreConfig = Path.Combine(Root, "LilithsHeart.cfg");

    /// <summary>
    /// Returns the path for a child module's .cfg file.
    /// e.g. HeartPathIndex.ModuleConfig("LilithsCookbook")
    ///      → BepInEx/config/LilithsHeart/LilithsCookbook.cfg
    /// </summary>
    public static string ModuleConfig(string moduleName)
        => Path.Combine(Root, $"{moduleName}.cfg");

    // ── Data subdirectories ─────────────────────────────────

    /// <summary>
    /// BepInEx/config/LilithsHeart/Aliases/
    /// Per-server prefab name alias overrides. One JSON file per
    /// LilithsMind *Index class (e.g. WeaponsIndex.json).
    /// Each file maps prefab string → custom alias name.
    /// Loaded by PrefabNameResolver after compiled defaults.
    /// Admin aliases take priority over compiled Name fields.
    /// Prefab strings are always preserved as a fallback.
    ///
    /// [CHANGED] Added for the name alias system.
    /// </summary>
    public static readonly string AliasesDir = Path.Combine(Root, "Aliases");

    /// <summary>
    /// BepInEx/config/LilithsHeart/Items/
    /// All item override files — appearance and functional.
    /// Scanned recursively by ItemService. Admins can create
    /// subdirectories freely (e.g. Items/Currencies/, Items/Weapons/).
    /// </summary>
    public static readonly string ItemsDir = Path.Combine(Root, "Items");

    /// <summary>
    /// BepInEx/config/LilithsHeart/Localization/
    /// Per-language item name and description overrides.
    /// One subfolder per language — name must match LanguageCodeEnum exactly
    /// (e.g. Localization/Spanish/, Localization/SChinese/).
    /// Each subfolder scanned recursively for *.json files.
    /// Loaded by LocalizationFileService at world ready.
    ///
    /// [CHANGED] Added for multi-language localization support.
    /// </summary>
    public static readonly string LocalizationDir = Path.Combine(Root, "Localization");

    /// <summary>
    /// Returns the path for a named data subdirectory.
    /// e.g. HeartPathIndex.DataDir("Recipes")
    ///      → BepInEx/config/LilithsHeart/Recipes/
    ///
    /// The directory is NOT created here — call Directory.CreateDirectory()
    /// at the point of first use.
    /// </summary>
    public static string DataDir(string category)
        => Path.Combine(Root, category);
}