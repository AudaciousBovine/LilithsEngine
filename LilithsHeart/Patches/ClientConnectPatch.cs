// ============================================================
//  ClientConnectPatch — LilithsHeart
//  LilithsHeart/Patches/ClientConnectPatch.cs
//
//  Detects when a client successfully joins and delivers the
//  sync payload via the configured transport mode.
//
//  [CHANGED] Branches on HeartConfig.SyncMode:
//    ChunkPush  — existing behaviour: enqueues tiered chunks
//    HttpServer — sends [[LE::sync-url:<url>]] redirect sentinel
//    StaticUrl  — sends [[LE::sync-url:<configured-url>]] redirect
//
//  For HttpServer mode the URL is built from the server's local
//  IP and configured HttpPort. For StaticUrl it is taken directly
//  from HeartConfig.StaticSyncUrl.
//
//  If SyncFallbackToChunks = true and Soul reports a fetch failure
//  via [[LE::sync-fallback]], Heart enqueues chunks for that client
//  via SyncSender.EnqueueSyncTiers() at that point (handled in
//  a separate VCF command handler, not here).
//
//  [CHANGED] New character pending queue — OnUserConnected fires
//            during character creation before the character entity
//            exists and before CharacterName is set. When both are
//            absent this is silently queued into _pending.
//            SchedulerPatch calls DrainPending() each frame to retry
//            these users until their character entity becomes available,
//            eliminating the need for the player to reconnect after
//            creating a new character.
//            A null character entity on a named user is still logged
//            as a warning (genuine unexpected state).
//
//  [PERFORMANCE] Runs once per client connect. Redirect path sends
//                one entity instead of dozens — cheaper on connect.
//                DrainPending() is O(pending count) per frame —
//                zero during normal play. The pending set is only
//                populated during new character creation.
// ============================================================

using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Stunlock.Network;
using Unity.Entities;
using LilithsHeart.Config;
using LilithsHeart.Foundation;
using LilithsHeart.Network;
using LilithsMind.Data;
using LilithsMind.Network;

namespace LilithsHeart.Patches;

[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
internal static class ClientConnectPatch
{
    private const string LOG_SOURCE = "LilithsHeart.ClientConnectPatch";

    // [CHANGED] Pending entry — stores everything needed to send the payload
    // once the character entity becomes available. The redirect URL is captured
    // at queue time so DrainPending doesn't need the original netConnectionId.
    sealed record PendingEntry(
        Entity UserEntity,
        int    UserIndex,
        string RedirectUrl   // empty string = ChunkPush mode
    );

    // HashSet (keyed by userIndex) avoids duplicates if OnUserConnected
    // fires more than once for the same user before the character is ready.
    static readonly Dictionary<int, PendingEntry> _pending = new();

    // ── OnUserConnected ───────────────────────────────────────

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

            var serverClient       = __instance._ApprovedUsersLookup[userIndex];
            Entity userEntity      = serverClient.UserEntity;
            var user               = userEntity.Read<User>();
            Entity characterEntity = user.LocalCharacter.GetEntityOnServer();

            // Build the redirect URL now while we have netConnectionId.
            // Captured into PendingEntry so DrainPending can use it later.
            var redirectUrl = BuildRedirectUrl(netConnectionId);

            if (characterEntity == Entity.Null)
            {
                if (user.CharacterName.IsEmpty)
                {
                    // [CHANGED] Character creation — name and entity both absent.
                    // Queue for retry; DrainPending will send once entity is ready.
                    _pending[userIndex] = new PendingEntry(userEntity, userIndex, redirectUrl);
                    HeartLogger.Debug(LOG_SOURCE,
                        $"New character creation (userIndex {userIndex}) — " +
                        "payload queued until character entity is ready.");
                    return;
                }

                // Named user but no character entity — genuine unexpected state.
                HeartLogger.Warning(LOG_SOURCE,
                    $"Character entity null for user '{user.CharacterName}' — " +
                    "payload not sent. Client should reconnect.");
                return;
            }

            SendPayload(userEntity, characterEntity, userIndex,
                user.CharacterName.ToString(), redirectUrl);
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE, $"ClientConnectPatch failed: {ex.Message}");
        }
    }

    // ── Pending retry (called from SchedulerPatch each frame) ─

    /// <summary>
    /// Retries payload delivery for users whose character entity was not yet
    /// available when OnUserConnected fired. Called each frame by SchedulerPatch.
    ///
    /// [CHANGED] Added to support new character creation — OnUserConnected fires
    ///           before the character entity exists. Once the entity is realized,
    ///           the payload is sent automatically without requiring a reconnect.
    ///
    /// [PERFORMANCE] O(pending count) — zero during normal play.
    ///               Only populated during new character creation.
    /// </summary>
    internal static void DrainPending(ServerBootstrapSystem bootstrap)
    {
        if (_pending.Count == 0) return;

        var resolved = new List<int>();

        foreach (var (userIndex, entry) in _pending)
        {
            try
            {
                if (!Heart.EntityManager.Exists(entry.UserEntity)) continue;

                var user           = entry.UserEntity.Read<User>();
                Entity characterEntity = user.LocalCharacter.GetEntityOnServer();

                if (characterEntity == Entity.Null) continue;

                // Character entity is now ready — send and mark resolved.
                HeartLogger.Info(LOG_SOURCE,
                    $"Pending character '{user.CharacterName}' now ready — " +
                    "sending deferred sync payload.");

                SendPayload(entry.UserEntity, characterEntity, userIndex,
                    user.CharacterName.ToString(), entry.RedirectUrl);

                resolved.Add(userIndex);
            }
            catch (Exception ex)
            {
                HeartLogger.Warning(LOG_SOURCE,
                    $"DrainPending failed for userIndex {userIndex}: {ex.Message}");
                // Leave in pending — retry next frame.
            }
        }

        foreach (var idx in resolved)
            _pending.Remove(idx);
    }

    /// <summary>
    /// Returns true when there are pending users awaiting character entity
    /// resolution. Used by SchedulerPatch to skip DrainPending when idle.
    /// </summary>
    internal static bool HasPending => _pending.Count > 0;

    // ── Shared helpers ────────────────────────────────────────

    /// <summary>
    /// Builds the redirect URL for HttpServer and StaticUrl modes from the
    /// current connection context. Called at OnUserConnected time while
    /// netConnectionId is available, then stored in the PendingEntry so
    /// DrainPending can use it without needing the original connection id.
    /// Returns an empty string for ChunkPush mode (no URL needed).
    /// </summary>
    static string BuildRedirectUrl(NetConnectionId netConnectionId)
    {
        return HeartConfig.SyncMode switch
        {
            SyncModeEnum.HttpServer => $"http://{netConnectionId}:{HeartConfig.HttpPort}/sync",
            SyncModeEnum.StaticUrl  => HeartConfig.StaticSyncUrl ?? string.Empty,
            _                       => string.Empty,
        };
    }

    /// <summary>
    /// Sends the sync payload to a client via the configured transport.
    /// Shared between Postfix (immediate) and DrainPending (deferred).
    /// redirectUrl is empty for ChunkPush, populated for Http/Static modes.
    /// </summary>
    static void SendPayload(
        Entity userEntity,
        Entity characterEntity,
        int    userIndex,
        string characterName,
        string redirectUrl)
    {
        switch (HeartConfig.SyncMode)
        {
            case SyncModeEnum.ChunkPush:
                HeartLogger.Info(LOG_SOURCE,
                    $"[ChunkPush] '{characterName}' connected — enqueuing tiered sync payload.");
                SyncSender.EnqueueSyncTiers(userEntity, characterEntity, userIndex);
                break;

            case SyncModeEnum.HttpServer:
                if (string.IsNullOrWhiteSpace(redirectUrl))
                {
                    HeartLogger.Warning(LOG_SOURCE,
                        $"[HttpServer] '{characterName}' — redirect URL empty, " +
                        "falling back to ChunkPush.");
                    SyncSender.EnqueueSyncTiers(userEntity, characterEntity, userIndex);
                    break;
                }
                HeartLogger.Info(LOG_SOURCE,
                    $"[HttpServer] '{characterName}' connected — sending redirect to '{redirectUrl}'.");
                SyncSender.SendRedirect(userEntity, characterEntity, userIndex, redirectUrl);
                break;

            case SyncModeEnum.StaticUrl:
                if (string.IsNullOrWhiteSpace(redirectUrl))
                {
                    HeartLogger.Warning(LOG_SOURCE,
                        "[StaticUrl] SyncMode=StaticUrl but StaticSyncUrl is empty. " +
                        "Falling back to ChunkPush for this client.");
                    SyncSender.EnqueueSyncTiers(userEntity, characterEntity, userIndex);
                    break;
                }
                HeartLogger.Info(LOG_SOURCE,
                    $"[StaticUrl] '{characterName}' connected — sending redirect to '{redirectUrl}'.");
                SyncSender.SendRedirect(userEntity, characterEntity, userIndex, redirectUrl);
                break;

            default:
                HeartLogger.Warning(LOG_SOURCE,
                    $"Unknown SyncMode '{HeartConfig.SyncMode}' — falling back to ChunkPush.");
                SyncSender.EnqueueSyncTiers(userEntity, characterEntity, userIndex);
                break;
        }
    }
}