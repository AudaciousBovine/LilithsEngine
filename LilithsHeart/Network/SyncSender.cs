using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using LilithsHeart.Foundation;
using LilithsMind.Network;

// ============================================================
//  SyncSender — LilithsHeart
//  LilithsHeart/Network/SyncSender.cs
//
//  Enqueues tiered sync payload chunks into SyncQueue on client
//  connect. SchedulerPatch drains the queue at ChunksPerFrame
//  rate each server frame.
//
//  [CHANGED] No longer sends chunks immediately on connect.
//            Chunks are enqueued into SyncQueue so SchedulerPatch
//            can drain them at a controlled rate across frames.
//            This prevents large simultaneous-connect spikes from
//            creating thousands of entities in one frame.
//
//  [CHANGED] NetworkIds are now captured at ENQUEUE time and carried
//            through the queue, instead of being re-read off the
//            stored entities at SEND time. Because the queue spans
//            frames, a client could disconnect before the drain ran;
//            re-reading NetworkId off a destroyed entity throws and
//            would silently abort the drain, leaving chunks unsent.
//            Capturing the IDs up front (entities are valid at connect)
//            removes that failure. SyncQueue.Drain() additionally drops
//            entries whose client entity no longer exists.
//
//  Protocol per tier:
//  ───────────────────
//  [[LG:begin:T:N:CKSUM]]   — begin sentinel (tier, chunk count, checksum)
//  [[LG:T:NNNN]]<data>      — chunk (tier, zero-padded index, base64+gzip data)
//  [[LG:end:T:CKSUM]]       — end sentinel (tier, checksum)
//
//  Soul accumulates chunks per tier and decompresses on end sentinel.
//  Each tier is independent — Soul applies Critical before High arrives.
//
//  Transport:
//  ──────────
//  ChatMessageServerEvent with ServerChatMessageType.System.
//  Soul intercepts before messages reach the chat UI.
//  SendEventToUser routes each entity to the correct client.
//
//  [PERFORMANCE] EnqueueSyncTiers() runs once per connect — O(n)
//                over tier blobs to build message strings and enqueue.
//                Actual entity creation is deferred to SchedulerPatch
//                at ChunksPerFrame per frame — no connect-frame spike.
// ============================================================

namespace LilithsHeart.Network;

public static class SyncSender
{
    private const string LOG_SOURCE = "LilithsHeart.SyncSender";

    private const string BEGIN_PREFIX = "[[LG:begin:";
    private const string CHUNK_PREFIX = "[[LG:";
    private const string END_PREFIX   = "[[LG:end:";

    // [PERFORMANCE] Static readonly — allocated once, reused for every entity create.
    static readonly ComponentType[] _networkEventComponents =
    [
        ComponentType.ReadOnly(Il2CppType.Of<FromCharacter>()),
        ComponentType.ReadOnly(Il2CppType.Of<NetworkEventType>()),
        ComponentType.ReadOnly(Il2CppType.Of<SendNetworkEventTag>()),
        ComponentType.ReadOnly(Il2CppType.Of<ChatMessageServerEvent>()),
        ComponentType.ReadOnly(Il2CppType.Of<SendEventToUser>()),
    ];

    static readonly NetworkEventType _networkEventType = new()
    {
        IsAdminEvent = false,
        EventId      = NetworkEvents.EventId_ChatMessageServerEvent,
        IsDebugEvent = false,
    };

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Enqueues all tier blobs for a connecting client into SyncQueue.
    /// Tiers are enqueued in order (Critical first) so they arrive
    /// and are applied in priority order.
    /// Called from ClientConnectPatch.
    ///
    /// [CHANGED] Captures user/character NetworkIds here (entities are
    ///           valid at connect) and passes them into the queue so the
    ///           drain never re-reads them off a possibly-dead entity.
    ///
    /// [PERFORMANCE] Builds message strings and enqueues — no entity creation.
    ///               Entity creation is deferred to SchedulerPatch.Drain().
    /// </summary>
    public static void EnqueueSyncTiers(Entity userEntity, Entity characterEntity, int userIndex)
    {
        var blobs = SyncPayloadCache.GetAllTierBlobs().ToList();

        if (blobs.Count == 0)
        {
            HeartLogger.Warning(LOG_SOURCE,
                "No tier blobs cached — cannot send. Is Heart fully initialized?");
            return;
        }

        // Capture routing IDs now, while the entities are guaranteed valid.
        // These travel with the queue entry; the drain uses them directly.
        var userNetId      = userEntity.Read<NetworkId>();
        var characterNetId = characterEntity.Read<NetworkId>();

        int totalChunks = 0;

        foreach (var blob in blobs.OrderBy(b => (int)b.Tier))
        {
            var messages = BuildTierMessages(blob);
            SyncQueue.Enqueue(
                userEntity, characterEntity, userNetId, characterNetId, userIndex, messages);
            totalChunks += blob.ChunkCount + 2; // +2 for begin + end sentinels
        }

        HeartLogger.Info(LOG_SOURCE,
            $"Enqueued {totalChunks} message(s) across {blobs.Count} tier(s) " +
            $"for userIndex {userIndex}.");
    }

    /// <summary>
    /// Sends a single queued chunk entity immediately.
    /// Called by SchedulerPatch via SyncQueue.Drain().
    /// Must run on the server main thread (EntityManager not thread-safe).
    ///
    /// [CHANGED] Takes the pre-captured user/character NetworkIds rather
    ///           than re-reading them from the entities (which may have
    ///           been recycled). The caller (SyncQueue.Drain) has already
    ///           confirmed the user entity still exists.
    /// </summary>
    public static void SendQueuedChunk(
        Entity    userEntity,
        Entity    characterEntity,
        NetworkId userNetId,
        NetworkId characterNetId,
        int       userIndex,
        string    message)
    {
        var em = Heart.EntityManager;
        SendSystemMessage(
            em, userEntity, characterEntity, userNetId, characterNetId, userIndex, message);
    }

    // ── Internal ─────────────────────────────────────────────

    /// <summary>
    /// Builds the full sequence of messages for a tier blob:
    ///   [[LG:begin:T:N:CKSUM]]
    ///   [[LG:T:0000]]<chunk>
    ///   [[LG:T:0001]]<chunk>
    ///   ...
    ///   [[LG:end:T:CKSUM]]
    /// </summary>
    static IEnumerable<string> BuildTierMessages(TierBlobData blob)
    {
        int t = (int)blob.Tier;

        // Begin sentinel.
        yield return $"{BEGIN_PREFIX}{t}:{blob.ChunkCount}:{blob.Checksum}]]";

        // Chunks with zero-padded index.
        for (int i = 0; i < blob.Chunks.Length; i++)
            yield return $"{CHUNK_PREFIX}{t}:{i:D4}]]{blob.Chunks[i]}";

        // End sentinel.
        yield return $"{END_PREFIX}{t}:{blob.Checksum}]]";
    }

    static void SendSystemMessage(
        EntityManager em,
        Entity userEntity,
        Entity characterEntity,
        NetworkId userNetId,
        NetworkId characterNetId,
        int userIndex,
        string text)
    {
        // Defensive truncation — chunks are pre-sized but sentinels could be long.
        if (text.Length > 509) text = text[..509];

        ChatMessageServerEvent chatEvent = new()
        {
            MessageText   = new FixedString512Bytes(text),
            MessageType   = ServerChatMessageType.System,
            FromCharacter = characterNetId,   // [CHANGED] pre-captured, not re-read
            FromUser      = userNetId,
            TimeUTC       = DateTime.UtcNow.Ticks
        };

        Entity entity = em.CreateEntity(_networkEventComponents);
        entity.Write(new FromCharacter { Character = characterEntity, User = userEntity });
        entity.Write(_networkEventType);
        entity.Write(chatEvent);
        entity.Write(new SendEventToUser { UserIndex = userIndex });
    }
}