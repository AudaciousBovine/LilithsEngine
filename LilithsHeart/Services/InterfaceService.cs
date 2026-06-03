// ============================================================
//  InterfaceService — LilithsHeart
//  LilithsHeart/Services/InterfaceService.cs
//
//  Server-side owner of item Icon overrides.
//  Reads Icon entries from LilithItemConfig.AppearanceOverrides
//  and registers them with Heart for inclusion in the Critical
//  sync tier payload sent to Soul on connect.
//
//  Responsibility split:
//  ──────────────────────
//  LocalizationService owns: DisplayName, DescriptionText
//  InterfaceService    owns: Icon
//  ItemFunctionalService owns: StackSize
//
//  All three read from LilithItemConfig which is populated by
//  LocalizationService.Initialize() in one file pass. Services
//  are separated by concern, not by file source.
//
//  Why Icon is separate from LocalizationService:
//  ───────────────────────────────────────────────
//  Icon resolution (local PNG, in-game sprite, URL download)
//  is a distinct concern from localization key repointing.
//  Future expansion — animated icons, per-player icons, runtime
//  icon upload — belongs here, not mixed into the localization
//  pipeline. Soul-side, InterfaceService data is applied by
//  IconPatcher which has its own map-building and cache logic.
//
//  Heart integration:
//  ───────────────────
//  Icon values travel in ServerSyncPayload.ItemAppearanceOverrides
//  alongside DisplayName/DescriptionText (same LilithItemData DTO).
//  Heart.RegisterItemAppearanceOverrides() merges all three fields
//  into one payload dictionary — InterfaceService contributes the
//  Icon field to that merged set.
//
//  Called from Heart.OnInitialize() after LocalizationService.
//
//  [PERFORMANCE] Runs once at startup. O(icon overrides) — reads
//                only entries that have a non-null Icon field.
//                No per-frame cost.
// ============================================================

using LilithsHeart.Config;
using LilithsHeart.Foundation;

namespace LilithsHeart.Services;

public static class InterfaceService
{
    private const string LOG_SOURCE = "LilithsHeart.InterfaceService";

    /// <summary>
    /// Reads Icon overrides from LilithItemConfig and logs a summary.
    /// Icon values are already in LilithItemConfig.AppearanceOverrides
    /// alongside DisplayName and DescriptionText — Heart's payload builder
    /// reads the full LilithItemData per entry, so no separate registration
    /// step is needed here. This method exists as the explicit ownership
    /// boundary and diagnostic point for icon-related startup activity.
    ///
    /// Called from Heart.OnInitialize() after LocalizationService.Initialize().
    /// </summary>
    public static void Initialize()
    {
        int iconCount = LilithItemConfig.AppearanceOverrides
            .Count(kvp => kvp.Value.Icon is not null);

        if (iconCount == 0)
        {
            HeartLogger.Info(LOG_SOURCE, "No Icon overrides configured.");
            return;
        }

        HeartLogger.Info(LOG_SOURCE,
            $"InterfaceService ready — {iconCount} Icon override(s) will be sent to Soul.");

        if (HeartConfig.IsDebug)
        {
            foreach (var (key, data) in LilithItemConfig.AppearanceOverrides)
            {
                if (data.Icon is null) continue;
                HeartLogger.Debug(LOG_SOURCE, $"  Icon override: '{key}' → '{data.Icon}'");
            }
        }
    }
}