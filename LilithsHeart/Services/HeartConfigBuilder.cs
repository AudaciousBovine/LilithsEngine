// ============================================================
//  HeartConfigBuilder — LilithsHeart
//  LilithsHeart/Services/HeartConfigBuilder.cs
//
//  Coordinates all config file generation for the suite.
//  Called from Heart.OnInitialize() before ItemService loads.
//
//  Generation paths:
//  ──────────────────
//  GenerateHeartExamples (HeartConfig flag):
//    Extracts Items/Examples_Item.json from embedded resources.
//    Always overwrites.
//
//  GenerateAllModuleExamples (HeartConfig flag):
//    Extracts Items/Examples_Item.json from embedded resources,
//    then calls each registered module example generator so
//    module-specific files are also written.
//    Always overwrites. Takes priority over GenerateHeartExamples.
//
//  GenerateDebugConfigs (HeartConfig flag):
//    Extracts Items/Debug_Item.json from embedded resources,
//    then calls each registered module debug generator.
//    Always overwrites.
//
//  Module registration:
//  ─────────────────────
//  Modules call these in Load() before Heart initializes:
//    RegisterExampleGenerator(Action) — module's own example
//      file generator, called by GenerateAllModuleExamples
//    RegisterDebugGenerator(Action)   — module's own debug
//      file generator, called by GenerateDebugConfigs
//
//  [CHANGED] Full simplification — embedded JSON resources replace
//            all C# dictionary-based example/debug builders.
//            RegisterItemExamples(), RegisterItemDebug(), and all
//            merge logic removed — ItemService's per-field file
//            merge handles module coexistence automatically.
//            Each module writes its own Items/*.json file;
//            no merging needed at generation time.
//
//  [PERFORMANCE] Zero cost on normal boots — all work gated behind
//                config flag checks. Resource extraction is O(file size).
// ============================================================

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using LilithsMind.Data;
using LilithsHeart.Config;
using LilithsHeart.Foundation;

namespace LilithsHeart.Services;

public static class HeartConfigBuilder
{
    private const string LOG_SOURCE    = "LilithsHeart.HeartConfigBuilder";
    private const string ASSEMBLY_NAME = "LilithsHeart";

    // Module example file generators — called by GenerateAllModuleExamples.
    static readonly List<Action> _exampleGenerators = [];

    // Module debug file generators — called by GenerateDebugConfigs.
    static readonly List<Action> _debugGenerators = [];

    // ── Registration API ─────────────────────────────────────

    /// <summary>
    /// Registers a module's example file generator.
    /// Called by GenerateAllModuleExamples after writing Examples_Item.json.
    /// Call from module Load() before Heart initializes.
    /// </summary>
    public static void RegisterExampleGenerator(Action generator)
    {
        if (generator != null)
            _exampleGenerators.Add(generator);
    }

    /// <summary>
    /// Registers a module's debug file generator.
    /// Called by GenerateDebugConfigs after writing Debug_Item.json.
    /// Call from module Load() before Heart initializes.
    /// </summary>
    public static void RegisterDebugGenerator(Action generator)
    {
        if (generator != null)
            _debugGenerators.Add(generator);
    }

    // ── Generation entry points ───────────────────────────────

    /// <summary>
    /// Checks all generation flags and runs the appropriate generators.
    /// Called from Heart.OnInitialize() before ItemService.
    ///
    /// GenerateAllModuleExamples takes priority over GenerateHeartExamples
    /// when both are set on the same boot.
    /// </summary>
    public static void RunIfRequested()
    {
        if (HeartConfig.GenerateAllModuleExamples)
        {
            GenerateAllModuleExamples();
            HeartConfig.DisableGenerateAllModuleExamples();

            if (HeartConfig.GenerateHeartExamples)
                HeartConfig.DisableGenerateHeartExamples();
        }
        else if (HeartConfig.GenerateHeartExamples)
        {
            GenerateHeartItemExamples();
            HeartConfig.DisableGenerateHeartExamples();
        }

        if (HeartConfig.GenerateDebugConfigs)
        {
            GenerateDebugConfigs();
            HeartConfig.DisableGenerateDebugConfigs();
        }
    }

    // ── Heart example generation ──────────────────────────────

    /// <summary>
    /// Extracts Items/Examples_Item.json from embedded resources.
    /// Always overwrites.
    /// </summary>
    public static void GenerateHeartItemExamples()
    {
        var path = Path.Combine(HeartPathIndex.ItemsDir, "Examples_Item.json");
        Directory.CreateDirectory(HeartPathIndex.ItemsDir);
        ExtractResource(ASSEMBLY_NAME, "Examples_Item.json", path);
        HeartLogger.Info(LOG_SOURCE, "Generated Items/Examples_Item.json.");
    }

    // ── All module examples ───────────────────────────────────

    /// <summary>
    /// Extracts Items/Examples_Item.json from embedded resources, then
    /// calls each registered module example generator.
    /// Always overwrites.
    /// </summary>
    static void GenerateAllModuleExamples()
    {
        var path = Path.Combine(HeartPathIndex.ItemsDir, "Examples_Item.json");
        Directory.CreateDirectory(HeartPathIndex.ItemsDir);
        ExtractResource(ASSEMBLY_NAME, "Examples_Item.json", path);
        HeartLogger.Info(LOG_SOURCE,
            $"Generated Items/Examples_Item.json — " +
            $"calling {_exampleGenerators.Count} module generator(s).");

        foreach (var generator in _exampleGenerators)
        {
            try { generator(); }
            catch (Exception ex)
            {
                HeartLogger.Error(LOG_SOURCE, $"Example generator failed: {ex.Message}");
            }
        }
    }

    // ── Debug config generation ───────────────────────────────

    /// <summary>
    /// Extracts Items/Debug_Item.json from embedded resources, then
    /// calls each registered module debug generator.
    /// Always overwrites.
    /// </summary>
    static void GenerateDebugConfigs()
    {
        var path = Path.Combine(HeartPathIndex.ItemsDir, "Debug_Item.json");
        Directory.CreateDirectory(HeartPathIndex.ItemsDir);
        ExtractResource(ASSEMBLY_NAME, "Debug_Item.json", path);
        HeartLogger.Info(LOG_SOURCE,
            $"Generated Items/Debug_Item.json — " +
            $"calling {_debugGenerators.Count} module debug generator(s).");

        foreach (var generator in _debugGenerators)
        {
            try { generator(); }
            catch (Exception ex)
            {
                HeartLogger.Error(LOG_SOURCE, $"Debug generator failed: {ex.Message}");
            }
        }
    }

    // ── Resource extraction ───────────────────────────────────

    /// <summary>
    /// Extracts an embedded JSON resource to a file path.
    /// Resource name format: {assemblyName}.Resources.{fileName}
    /// Always overwrites the target file.
    ///
    /// [PERFORMANCE] Stream read once per generation trigger — negligible.
    /// </summary>
    public static void ExtractResource(string assemblyName, string fileName, string outputPath)
    {
        var resourceName = $"{assemblyName}.Resources.{fileName}";
        var assembly     = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            HeartLogger.Error(LOG_SOURCE,
                $"Embedded resource '{resourceName}' not found. " +
                "Ensure the file is marked as EmbeddedResource in the .csproj.");
            return;
        }

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        File.WriteAllText(outputPath, json);

        HeartLogger.Debug(LOG_SOURCE,
            $"Extracted '{resourceName}' → '{Path.GetFileName(outputPath)}'.");
    }
}