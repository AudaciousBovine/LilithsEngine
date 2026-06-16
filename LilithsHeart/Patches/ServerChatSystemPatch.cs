// ============================================================
//  ServerChatSystemPatch — LilithsHeart
//  LilithsHeart/Patches/ServerChatSystemPatch.cs
//
//  General server-side intercept for Soul→Heart chat messages.
//  Mirrors ClientChatSystemPatch on Soul's side — Soul sends
//  [[LE::...]] sentinels as plain chat messages (no VCF dependency)
//  and this patch intercepts and consumes them before they reach
//  the broadcast system.
//
//  Current sentinels handled:
//  ───────────────────────────
//  [[LE::sync-fallback]] — Soul failed an HTTP sync fetch and
//    SyncFallbackToChunks = true. Heart enqueues chunk delivery
//    for that specific client.
//
//  Future sentinels can be added here as new Soul→Heart
//  communication needs arise (proximity events, panel interactions,
//  etc.) without requiring VCF on Soul.
//
//  Why chat messages and not VCF:
//  ────────────────────────────────
//  Soul has no VCF dependency and should not gain one — VCF is
//  server-side only. Soul sends [[LE::...]] messages using
//  ChatMessageClientEvent in the client ECS world, the same
//  mechanism the game uses for all player chat.
//
//  Hook target: ServerBootstrapSystem.OnUpdate (postfix)
//  ──────────────────────────────────────────────────────
//  There is no accessible ChatMessageServerSystem type in the
//  V Rising server assemblies. Instead we hook the same system
//  used by SchedulerPatch and query for ChatMessageServerEvent
//  entities with a FromCharacter component — these are incoming
//  player messages received by the server.
//
//  Postfix (not prefix) — we read the entities after the server's
//  own chat processing runs, then destroy consumed ones. This avoids
//  interfering with VCF's own chat intercept ordering.
//
//  [PERFORMANCE] Per-frame cost is negligible — the query is
//                empty except when a client sends a message.
//                The [[LE:: prefix check short-circuits immediately
//                on all normal player chat.
// ============================================================

using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using LilithsHeart.Config;
using LilithsHeart.Foundation;
using LilithsHeart.Network;
using LilithsHeart.Services;

namespace LilithsHeart.Patches;

[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUpdate))]
internal static class ServerChatSystemPatch
{
    private const string LOG_SOURCE          = "LilithsHeart.ServerChatSystemPatch";
    private const string FALLBACK_SENTINEL   = "[[LE::sync-fallback]]";
    private const string LANG_REQUEST_PREFIX = "[[LE::lang-request:";

    // EntityQuery for incoming player chat messages.
    // Lazily initialized on first use.
    static EntityQuery _chatQuery;
    static bool _queryBuilt;

    [HarmonyPostfix]
    static void Postfix(ServerBootstrapSystem __instance)
    {
        if (!Heart.IsReady) return;

        var em = Heart.EntityManager;

        // Build the query once — filter for entities that have both
        // ChatMessageServerEvent and FromCharacter (player-sent messages).
        if (!_queryBuilt)
        {
            _chatQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<ChatMessageServerEvent>(),
                ComponentType.ReadOnly<FromCharacter>());
            _queryBuilt = true;
        }

        var entities = _chatQuery.ToEntityArray(Allocator.Temp);
        if (entities.Length == 0)
        {
            entities.Dispose();
            return;
        }

        try
        {
            foreach (var entity in entities)
            {
                var chatEvent = em.GetComponentData<ChatMessageServerEvent>(entity);
                var message   = chatEvent.MessageText.ToString();

                // Fast-path: ignore anything that doesn't start with [[LE::
                if (!message.StartsWith("[[LE::", StringComparison.Ordinal)) continue;

                if (message.Equals(FALLBACK_SENTINEL, StringComparison.Ordinal))
                {
                    HandleSyncFallback(em, entity, __instance);
                }
                // [CHANGED] Language request from Soul — client wants a different
                //           language than the server default.
                else if (message.StartsWith(LANG_REQUEST_PREFIX, StringComparison.Ordinal))
                {
                    HandleLangRequest(em, entity, __instance, message);
                }
                // Future [[LE::...]] sentinels from Soul handled here.
            }
        }
        finally
        {
            entities.Dispose();
        }
    }

    // ── Sentinel handlers ─────────────────────────────────────

    static void HandleLangRequest(
        EntityManager em,
        Entity entity,
        ServerBootstrapSystem bootstrap,
        string message)
    {
        // [[LE::lang-request:Spanish]]
        var languageName = message[LANG_REQUEST_PREFIX.Length..^2];

        if (string.IsNullOrWhiteSpace(languageName))
        {
            HeartLogger.Warning(LOG_SOURCE, "Received malformed lang-request sentinel.");
            em.DestroyEntity(entity);
            return;
        }

        var fromCharacter   = em.GetComponentData<FromCharacter>(entity);
        var userEntity      = fromCharacter.User;
        var characterEntity = fromCharacter.Character;

        if (!em.Exists(userEntity) || !em.Exists(characterEntity))
        {
            HeartLogger.Warning(LOG_SOURCE,
                "Received lang-request but user/character entity no longer exists.");
            em.DestroyEntity(entity);
            return;
        }

        int userIndex = -1;
        for (int i = 0; i < bootstrap._ApprovedUsersLookup.Length; i++)
        {
            if (bootstrap._ApprovedUsersLookup[i].UserEntity == userEntity)
            {
                userIndex = i;
                break;
            }
        }

        if (userIndex < 0)
        {
            HeartLogger.Warning(LOG_SOURCE,
                $"Received lang-request for '{languageName}' but could not resolve userIndex.");
            em.DestroyEntity(entity);
            return;
        }

        var user = em.GetComponentData<User>(userEntity);
        HeartLogger.Info(LOG_SOURCE,
            $"[Lang] '{user.CharacterName}' requested language '{languageName}'.");

        LilithsHeart.Network.LocalizationSyncSender.HandleRequest(
            userEntity, characterEntity, userIndex, languageName);

        em.DestroyEntity(entity);
    }

    static void HandleSyncFallback(EntityManager em, Entity entity, ServerBootstrapSystem bootstrap)
    {
        var fromCharacter   = em.GetComponentData<FromCharacter>(entity);
        var userEntity      = fromCharacter.User;
        var characterEntity = fromCharacter.Character;

        if (!em.Exists(userEntity) || !em.Exists(characterEntity))
        {
            HeartLogger.Warning(LOG_SOURCE,
                "Received [[LE::sync-fallback]] but user/character entity no longer exists.");
            em.DestroyEntity(entity);
            return;
        }

        // Resolve userIndex by walking ApprovedUsersLookup.
        // [PERFORMANCE] O(n) over connected players — negligible count.
        int userIndex = -1;
        for (int i = 0; i < bootstrap._ApprovedUsersLookup.Length; i++)
        {
            if (bootstrap._ApprovedUsersLookup[i].UserEntity == userEntity)
            {
                userIndex = i;
                break;
            }
        }

        if (userIndex < 0)
        {
            HeartLogger.Warning(LOG_SOURCE,
                "Received [[LE::sync-fallback]] but could not resolve userIndex — ignoring.");
            em.DestroyEntity(entity);
            return;
        }

        var user = em.GetComponentData<User>(userEntity);
        HeartLogger.Info(LOG_SOURCE,
            $"[Fallback] HTTP fetch failed for '{user.CharacterName}' — " +
            "enqueuing chunk sync.");

        SyncSender.EnqueueSyncTiers(userEntity, characterEntity, userIndex);

        // Consume — do not broadcast to other players.
        em.DestroyEntity(entity);
    }
}