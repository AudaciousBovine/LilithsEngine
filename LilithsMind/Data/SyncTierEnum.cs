// ============================================================
//  SyncTierEnum — LilithsMind
//  LilithsMind/Network/SyncTierEnum.cs
//
//  Priority tiers for the Heart→Soul sync payload delivery
//  system. Lower numeric value = higher priority = sent first.
//
//  This is a shared wire contract: Heart stamps the tier's
//  integer value into every sentinel ([[LG:begin:T:...]],
//  [[LG:T:NNNN]], [[LG:end:T:...]]) and Soul reads it back to
//  route each decoded slice. It therefore belongs in LilithsMind
//  alongside the other shared DTOs, not in either plugin.
//
//  [CHANGED] Consolidated into LilithsMind. Previously the live
//            enum lived in LilithsHeart/Network/SyncTierEnum.cs
//            (server-only), forcing LilithsSoul — which cannot
//            reference Heart — to duplicate the values as local
//            constants. That Heart file is now deleted and both
//            sides reference this single definition.
//
//  [CHANGED] Removed the divergent, unused SyncTier enum and
//            TierAssignments class that previously occupied this
//            file. They were 1-based, lacked Background, and were
//            never referenced by the transport (which has always
//            used the 0-based values below). TierAssignments had
//            also drifted stale (PlayerRecipes mis-tiered as High;
//            referenced the retired LilithsVision). Kept the
//            0-based values so no sentinel on the wire changes.
//
//  Tier assignment rationale:
//  ───────────────────────────
//  Critical   — ItemAppearanceOverrides (display names, tooltips,
//               icons). Must arrive before the inventory UI builds
//               or the player sees vanilla names briefly.
//               Smallest data volume — typically under 5KB.
//
//  High       — RecipeOverrides + StationRecipeOverrides.
//               Crafting UI is visible shortly after login.
//               Medium data volume.
//
//  Normal     — PlayerRecipesToAdd + PlayerRecipesToRemove.
//               Player recipe unlocks are less time-sensitive —
//               a brief delay before they appear is acceptable.
//
//  Low        — Reserved for future modules: quest names/text
//               (LilithsMachinations), spell names/tooltips
//               (LilithsGrimoire). Background apply acceptable.
//
//  Background — Reserved for large data sets with no urgency:
//               horse breeding tables (LilithsMenagerie),
//               bounty data (LilithsBounty), treasury config
//               (LilithsTreasury). Sent last, applied lazily.
//
//  [PERFORMANCE] Tiers are processed in ascending numeric order
//                by SyncSender. Critical arrives and is applied
//                by Soul before High chunks even begin sending.
//                Each tier is independent — Soul applies each on
//                its own end sentinel without waiting for later
//                tiers to arrive.
// ============================================================

namespace LilithsMind.Data;

public enum SyncTierEnum
{
    Critical   = 0,
    High       = 1,
    Normal     = 2,
    Low        = 3,
    Background = 4,
}