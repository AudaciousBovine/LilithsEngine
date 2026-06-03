// ============================================================
//  CookbookPlugin — LilithsCookbook
//  LilithsCookbook/CookbookPlugin.cs
//
//  BepInEx entry point for LilithsCookbook.
//
//  [CHANGED] StationData property removed. CookbookStationData
//            is retired — station membership is now embedded in
//            RecipeEntryData.Stations. StationSystem no longer
//            needs a separate data container.
//
//  [CHANGED] PrisonerFeedData property added. Loaded alongside
//            RecipeData from the same Recipes/*.json files via
//            the updated CookbookLoader.LoadRecipes() signature
//            which now returns a tuple.
//
//  [CHANGED] OnHeartInitialized() no longer calls
//            CookbookLoader.LoadStations() or passes StationData
//            to StationSystem. StationSystem.ApplyChanges() reads
//            from CookbookPlugin.RecipeData directly.
//
//  [CHANGED] PrisonerFeedSystem.ApplyChanges() added to the
//            initialization sequence after RecipeSystem. Stubbed
//            until ECS component names are verified.
//
//  All MyPluginInfo references fully qualified as
//  LilithsCookbook.MyPluginInfo to avoid namespace conflict with
//  LilithsHeart.MyPluginInfo (both in scope via ProjectReference).
// ============================================================

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using LilithsHeart.Config;
using LilithsHeart.Foundation;
using LilithsHeart.Modules;
using LilithsCookbook.Config;
using LilithsCookbook.Data;
using LilithsCookbook.Systems;

namespace LilithsCookbook;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("audaciousbovine.lilithsheart")]
public class CookbookPlugin : BasePlugin
{
    private const string LOG_SOURCE = "LilithsCookbook";

    /// <summary>
    /// Loaded recipe overrides. Populated in OnHeartInitialized.
    /// Read by RecipeSystem and StationSystem.
    /// </summary>
    public static CookbookRecipeData? RecipeData { get; private set; }

    /// <summary>
    /// [CHANGED] Loaded prisoner feeding overrides.
    /// Populated alongside RecipeData from the same Recipes/*.json files.
    /// Read by PrisonerFeedSystem.
    /// </summary>
    public static CookbookPrisonerFeedData? PrisonerFeedData { get; private set; }

    // [CHANGED] StationData removed — station membership is now inlined
    //           into RecipeEntryData.Stations. StationSystem reads RecipeData.

    public override void Load()
    {
        HeartLogger.Info(LOG_SOURCE,
            $"{LilithsCookbook.MyPluginInfo.PLUGIN_NAME} v{LilithsCookbook.MyPluginInfo.PLUGIN_VERSION} loading.");

        var configFile = new ConfigFile(
            HeartPathIndex.ModuleConfig("LilithsCookbook"), saveOnInit: true);

        CookbookConfig.Initialize(configFile);

        // [CHANGED] Initialize() no longer creates Stations/ directory or
        //           writes example-stations.json — only Recipes/ and its example.
        CookbookConfigBuilder.Initialize();

        HeartModuleRegistry.Register(new HeartModuleData
        {
            ModuleId   = LilithsCookbook.MyPluginInfo.PLUGIN_GUID,
            ModuleName = LilithsCookbook.MyPluginInfo.PLUGIN_NAME,
            Version    = LilithsCookbook.MyPluginInfo.PLUGIN_VERSION,
        });

        Heart.OnInitialized += OnHeartInitialized;
    }

    public override bool Unload()
    {
        Heart.OnInitialized -= OnHeartInitialized;
        HeartLogger.Info(LOG_SOURCE, $"{LilithsCookbook.MyPluginInfo.PLUGIN_NAME} unloaded.");
        return true;
    }

    static void OnHeartInitialized()
    {
        // Generate all-recipes.json if the flag is set — runs once, then disables itself.
        CookbookConfigBuilder.GenerateAllRecipesIfRequested();

        // [CHANGED] Generate prisoner-feed-example.json if the flag is set.
        // Runs before LoadRecipes() so the generated file is picked up on the same boot.
        CookbookConfigBuilder.GeneratePrisonerFeedExampleIfRequested();

        // [CHANGED] LoadRecipes() now returns a tuple (recipes, feeding).
        //           LoadStations() call removed.
        (RecipeData, PrisonerFeedData) =
            CookbookLoader.LoadRecipes(CookbookConfigBuilder.RecipesDir);

        // Apply recipe changes to ECS + register overrides for Soul sync.
        RecipeSystem.ApplyChanges();

        // [CHANGED] StationSystem.ApplyChanges() no longer receives StationData.
        //           It reads CookbookPlugin.RecipeData and derives station
        //           membership from each entry's Stations list.
        StationSystem.ApplyChanges();

        // [CHANGED] PrisonerFeedSystem.ApplyChanges() wired in.
        //           Currently stubbed — will log what it would do until
        //           ECS component names are confirmed.
        PrisonerFeedSystem.ApplyChanges();
    }
}