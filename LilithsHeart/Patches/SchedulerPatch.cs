// ============================================================
//  SchedulerPatch — LilithsHeart
//  LilithsHeart/Patches/SchedulerPatch.cs
//
//  Drains SyncQueue at a controlled rate each server frame.
//  Calls SyncQueue.Drain() which creates at most
//  SyncQueue.ChunksPerFrame ECS entities per frame.
//
//  Hook target: ServerBootstrapSystem.OnUpdate (postfix)
//  ──────────────────────────────────────────────────────
//  ServerBootstrapSystem.OnUpdate runs every server frame and
//  is the established hook point for per-frame server work
//  in V Rising mods. Postfix ensures game logic runs first.
//
//  Why not a custom ECS system?
//  ─────────────────────────────
//  A Harmony patch on an existing system is simpler and avoids
//  the complexity of registering a custom ComponentSystemBase
//  in an IL2CPP environment. The per-frame cost is a single
//  bool check (HasPending) when the queue is empty — negligible.
//
//  [CHANGED] Now also calls ClientConnectPatch.DrainPending() each
//            frame to retry payload delivery for clients whose
//            character entity was not yet realized when
//            OnUserConnected fired (new character creation path).
//            DrainPending is gated on ClientConnectPatch.HasPending
//            so it is free during normal play.
//
//  [PERFORMANCE] When SyncQueue.HasPending is false (normal play),
//                cost is two bool reads per frame — effectively free.
//                During a connect event, at most ChunksPerFrame entity
//                creates occur per frame — bounded constant cost.
//                DrainPending is O(pending count) — zero during normal
//                play; only active during new character creation.
// ============================================================

using HarmonyLib;
using ProjectM;
using LilithsHeart.Foundation;
using LilithsHeart.Network;

namespace LilithsHeart.Patches;

[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUpdate))]
internal static class SchedulerPatch
{
    [HarmonyPostfix]
    static void Postfix(ServerBootstrapSystem __instance)
    {
        if (!Heart.IsReady) return;

        // Drain the sync chunk queue at a controlled rate.
        // Fast path — skip lock acquisition when nothing is pending.
        if (SyncQueue.HasPending)
            SyncQueue.Drain();

        // [CHANGED] Retry payload delivery for clients whose character entity
        // was not yet available when OnUserConnected fired (new character creation).
        // HasPending gate keeps this free during normal play.
        if (ClientConnectPatch.HasPending)
            ClientConnectPatch.DrainPending(__instance);
    }
}