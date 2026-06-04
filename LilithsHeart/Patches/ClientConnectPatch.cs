// ============================================================
//  ClientConnectPatch — LilithsHeart
//  LilithsHeart/Patches/ClientConnectPatch.cs
//
//  Detects when a client successfully joins and delivers the
//  sync payload via the configured transport mode.
//
//  [CHANGED] Branches on HeartConfig.SyncMode:
//    ChunkPush  — existing behaviour: enqueues tiered chunks
//    HttpServer — sends [[LG:sync-url:<url>]] redirect sentinel
//    StaticUrl  — sends [[LG:sync-url:<configured-url>]] redirect
//
//  For HttpServer mode the URL is built from the server's local
//  IP and configured HttpPort. For StaticUrl it is taken directly
//  from HeartConfig.StaticSyncUrl.
//
//  If SyncFallbackToChunks = true and Soul reports a fetch failure
//  via [[LG:sync-fallback]], Heart enqueues chunks for that client
//  via SyncSender.EnqueueSyncTiers() at that point (handled in
//  a separate VCF command handler, not here).
//
//  [PERFORMANCE] Runs once per client connect. Redirect path sends
//                one entity instead of dozens — cheaper on connect.
// ============================================================

using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Stunlock.Network;
using Unity.Entities;
using LilithsHeart.Config;
using LilithsHeart.Foundation;
using LilithsHeart.Network;
using LilithsMind.Data;    // SyncModeEnum, SyncTierEnum, LanguageCodeEnum
using LilithsMind.Network;

namespace LilithsHeart.Patches;

[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
internal static class ClientConnectPatch
{
    private const string LOG_SOURCE = "LilithsHeart.ClientConnectPatch";

    [HarmonyPostfix]
    static void Postfix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
    {
        try
        {
            if (!Heart.IsReady)
            {
                HeartLogger.Warning(LOG_SOURCE,
                    "Client connected before Heart was ready — sync not sent. " +
                    "Client should reconnect.");
                return;
            }

            if (!__instance._NetEndPointToApprovedUserIndex.TryGetValue(
                    netConnectionId, out int userIndex))
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"Could not resolve connection {netConnectionId} to user index.");
                return;
            }

            var serverClient  = __instance._ApprovedUsersLookup[userIndex];
            Entity userEntity = serverClient.UserEntity;

            var user = userEntity.Read<User>();
            Entity characterEntity = user.LocalCharacter.GetEntityOnServer();

            if (characterEntity == Entity.Null)
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"Character entity null for user {user.CharacterName} — " +
                    "payload deferred. Client should reconnect.");
                return;
            }

            // [CHANGED] Branch on configured sync transport mode.
            switch (HeartConfig.SyncMode)
            {
                case SyncModeEnum.ChunkPush:
                    HeartLogger.Info(LOG_SOURCE,
                        $"[ChunkPush] '{user.CharacterName}' connected — enqueuing tiered sync payload.");
                    SyncSender.EnqueueSyncTiers(userEntity, characterEntity, userIndex);
                    break;

                case SyncModeEnum.HttpServer:
                    // Build the URL from the server's accessible address + configured port.
                    // Uses the connection's local endpoint IP so the URL is reachable
                    // from outside the server process.
                    var httpUrl = $"http://{netConnectionId}:{HeartConfig.HttpPort}/sync";
                    HeartLogger.Info(LOG_SOURCE,
                        $"[HttpServer] '{user.CharacterName}' connected — sending redirect to '{httpUrl}'.");
                    SyncSender.SendRedirect(userEntity, characterEntity, userIndex, httpUrl);
                    break;

                case SyncModeEnum.StaticUrl:
                    var staticUrl = HeartConfig.StaticSyncUrl;
                    if (string.IsNullOrWhiteSpace(staticUrl))
                    {
                        HeartLogger.Warning(LOG_SOURCE,
                            $"[StaticUrl] SyncMode=StaticUrl but StaticSyncUrl is empty. " +
                            "Falling back to ChunkPush for this client.");
                        SyncSender.EnqueueSyncTiers(userEntity, characterEntity, userIndex);
                        break;
                    }
                    HeartLogger.Info(LOG_SOURCE,
                        $"[StaticUrl] '{user.CharacterName}' connected — sending redirect to '{staticUrl}'.");
                    SyncSender.SendRedirect(userEntity, characterEntity, userIndex, staticUrl);
                    break;

                default:
                    HeartLogger.Warning(LOG_SOURCE,
                        $"Unknown SyncMode '{HeartConfig.SyncMode}' — falling back to ChunkPush.");
                    SyncSender.EnqueueSyncTiers(userEntity, characterEntity, userIndex);
                    break;
            }
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE, $"ClientConnectPatch failed: {ex.Message}");
        }
    }
}