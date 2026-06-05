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
//  [CHANGED] RecipeSystem.ApplyMapValues() now called LAST in the
//            init sequence — after StationSystem. Both RecipeSystem
//            and StationSystem call RegisterRecipes()/RegisterGameData()
//            which rebuild RecipeHashLookupMap from baked data, wiping
//            any earlier scalar-field writes (CraftDuration, etc.).
//            Writing the map last ensures custom durations survive and
//            the crafting completion system reads the intended values.
//            This was the root cause of the "24 hour timer" bug on
//            recipes moved into player crafting.
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
using LilithsMind.Data;
using LilithsHeart.Services;
using LilithsCookbook.Config;
using LilithsCookbook.Data;
using LilithsCookbook.Services;
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
        // Initialize config first so ModuleEnabled can be read.
        var configFile = new ConfigFile(
            HeartPathIndex.ModuleConfig("LilithsCookbook"), saveOnInit: true);

        CookbookConfig.Initialize(configFile);

        // [CHANGED] ModuleEnabled check — skip all initialization if false.
        //           No ECS patching, no Heart registration, no subscriptions.
        if (!CookbookConfig.ModuleEnabled)
        {
            HeartLogger.Info(LOG_SOURCE,
                $"{LilithsCookbook.MyPluginInfo.PLUGIN_NAME} is disabled via ModuleEnabled=false. Skipping.");
            return;
        }

        HeartLogger.Info(LOG_SOURCE,
            $"{LilithsCookbook.MyPluginInfo.PLUGIN_NAME} v{LilithsCookbook.MyPluginInfo.PLUGIN_VERSION} loading.");

        // [CHANGED] Initialize() no longer creates Stations/ directory or
        //           writes example-stations.json — only Recipes/ and its example.
        CookbookConfigBuilder.Initialize();

        // [CHANGED] RegisterItemExamples and RegisterItemDebug removed — Cookbook
        //           now writes its own Items/Examples_CookbookItem.json and
        //           Items/Debug_CookbookItem.json from embedded resources.
        //           ItemService merges all Items/*.json files automatically,
        //           so no code-level merging is needed.
        HeartConfigBuilder.RegisterExampleGenerator(CookbookConfigBuilder.GenerateExampleFiles);
        HeartConfigBuilder.RegisterDebugGenerator(CookbookConfigBuilder.GenerateDebugFiles);

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
        // Generate AllRecipes.json dump if requested.
        CookbookConfigBuilder.GenerateAllRecipesIfRequested();

        // [CHANGED] GenerateCookbookExamples — generates all four Cookbook example files.
        if (CookbookConfig.GenerateCookbookExamples)
        {
            CookbookConfigBuilder.GenerateExampleFiles();
            CookbookConfig.DisableGenerateCookbookExamples();
        }

        // [CHANGED] GenerateCookbookDebugConfigs — generates all four Cookbook debug files.
        if (CookbookConfig.GenerateCookbookDebugConfigs)
        {
            CookbookConfigBuilder.GenerateDebugFiles();
            CookbookConfig.DisableGenerateCookbookDebugConfigs();
        }

        // [CHANGED] LoadRecipes() now returns a tuple (recipes, feeding).
        //           LoadStations() call removed.
        (RecipeData, PrisonerFeedData) =
            CookbookLoader.LoadRecipes(CookbookConfigBuilder.RecipesDir);

        // Apply recipe changes to ECS + register overrides for Soul sync.
        // (entity writes + buffers + RegisterRecipes + Soul override registration)
        RecipeSystem.ApplyChanges();

        // [CHANGED] StationSystem.ApplyChanges() no longer receives StationData.
        //           It reads CookbookPlugin.RecipeData and derives station
        //           membership from each entry's Stations list.
        //           NOTE: StationSystem calls RegisterRecipes() + RegisterGameData()
        //           which rebuild RecipeHashLookupMap — so the map scalar writes
        //           must happen AFTER this (see ApplyMapValues below).
        StationSystem.ApplyChanges();

        // [CHANGED] ItemFunctionService applies StackSize overrides
        //           from LilithItemConfig to ECS prefab entities.
        ItemFunctionService.ApplyOverrides();

        // [CHANGED] PrisonerFeedSystem.ApplyChanges() wired in.
        //           Currently stubbed — will log what it would do until
        //           ECS component names are confirmed.
        PrisonerFeedSystem.ApplyChanges();

        // [CHANGED] FINAL step — write recipe scalar fields (CraftDuration,
        //           AlwaysUnlocked, etc.) to RecipeHashLookupMap LAST, after
        //           every RegisterRecipes()/RegisterGameData() call from both
        //           RecipeSystem and StationSystem. The crafting completion
        //           system reads CraftDuration from this map; writing it last
        //           ensures custom durations survive and are not reset to vanilla
        //           (the root cause of the 24-hour timer bug).
        RecipeSystem.ApplyMapValues();
    }
}