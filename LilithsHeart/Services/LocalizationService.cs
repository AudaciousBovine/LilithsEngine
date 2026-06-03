// ============================================================
//  LocalizationService — LilithsHeart
//  LilithsHeart/Services/LocalizationService.cs
//
//  Applies DisplayName and DescriptionText overrides from
//  LilithItemConfig.Overrides to the sync payload.
//
//  Responsibility:
//  ────────────────
//  LocalizationService owns DisplayName and DescriptionText.
//  It reads from LilithItemConfig which is populated by
//  ItemService — no file I/O occurs here.
//
//  Service boundaries:
//    ItemService         — file I/O, JSON parsing, LilithItemConfig population
//    LocalizationService — owns DisplayName, DescriptionText (apply layer)
//    InterfaceService    — owns Icon (apply layer)
//    ItemFunctionalService — owns StackSize (ECS patching, server-only)
//
//  [CHANGED] File loading responsibility fully moved to ItemService.
//            RegisterDirectory(), Load(), LoadFile() all removed.
//            This service is now a pure apply-layer — it reads from
//            LilithItemConfig and logs a summary of what it owns.
//            No file I/O, no directory registration, no JSON parsing.
//
//  [PERFORMANCE] Zero file I/O. Runs once at world ready.
//                Reading from LilithItemConfig is O(1) per entry.
// ============================================================

using LilithsHeart.Config;
using LilithsHeart.Foundation;

namespace LilithsHeart.Services;

public static class LocalizationService
{
    private const string LOG_SOURCE = "LilithsHeart.LocalizationService";

    /// <summary>
    /// Reads DisplayName and DescriptionText overrides from
    /// LilithItemConfig and logs a summary.
    ///
    /// These values are already in LilithItemConfig.Overrides
    /// populated by ItemService. Heart's payload builder reads the full
    /// LilithItemData per entry — no separate registration step needed.
    /// This method is the explicit ownership boundary and diagnostic
    /// point for localization-related startup activity.
    ///
    /// Called from Heart.OnInitialize() after ItemService.Initialize().
    /// </summary>
    public static void Initialize()
    {
        int nameCount = LilithItemConfig.Overrides
            .Count(kvp => kvp.Value.DisplayName is not null);

        int descCount = LilithItemConfig.Overrides
            .Count(kvp => kvp.Value.DescriptionText is not null);

        if (nameCount == 0 && descCount == 0)
        {
            HeartLogger.Info(LOG_SOURCE,
                "No DisplayName or DescriptionText overrides configured.");
            return;
        }

        HeartLogger.Info(LOG_SOURCE,
            $"LocalizationService ready — " +
            $"{nameCount} DisplayName override(s), " +
            $"{descCount} DescriptionText override(s) will be sent to Soul.");

        if (HeartConfig.IsDebug)
        {
            foreach (var (key, data) in LilithItemConfig.Overrides)
            {
                if (data.DisplayName is not null)
                    HeartLogger.Debug(LOG_SOURCE,
                        $"  DisplayName: '{key}' → '{data.DisplayName}'");

                if (data.DescriptionText is not null)
                    HeartLogger.Debug(LOG_SOURCE,
                        $"  DescriptionText: '{key}' → '{data.DescriptionText}'");
            }
        }
    }
}