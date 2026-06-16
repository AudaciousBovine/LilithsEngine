using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using LilithsHeart.Config;
using LilithsHeart.Foundation;
using LilithsMind.Data;

namespace LilithsHeart.Network;

public static class SyncSender
{
    private const string LOG_SOURCE = "LilithsHeart.SyncSender";

    private const string BEGIN_PREFIX    = "[[LE::begin:";
    private const string CHUNK_PREFIX    = "[[LE::";
    private const string END_PREFIX      = "[[LE::end:";
    private const string REDIRECT_PREFIX = "[[LE::sync-url:";

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

    public static void EnqueueSyncTiers(Entity userEntity, Entity characterEntity, int userIndex)
    {
        var blobs = SyncPayloadCache.GetAllTierBlobs().ToList();

        if (blobs.Count == 0)
        {
            HeartLogger.Warning(LOG_SOURCE,
                "No tier blobs cached — cannot send. Is Heart fully initialized?");
            return;
        }

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

    public static void SendRedirect(
        Entity userEntity,
        Entity characterEntity,
        int    userIndex,
        string url)
    {
        var userNetId      = userEntity.Read<NetworkId>();
        var characterNetId = characterEntity.Read<NetworkId>();
        var fallback       = HeartConfig.SyncFallbackToChunks ? "1" : "0";
        var message        = $"{REDIRECT_PREFIX}{url}:{fallback}]]";

        SendSystemMessage(
            Heart.EntityManager,
            userEntity, characterEntity,
            userNetId, characterNetId,
            userIndex, message);

        HeartLogger.Debug(LOG_SOURCE,
            $"Sent redirect sentinel to userIndex {userIndex}: url='{url}' fallback={fallback}");
    }

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
    ///   [[LE::begin:T:N:CKSUM]]
    ///   [[LE::T:0000]]<chunk>
    ///   [[LE::T:0001]]<chunk>
    ///   ...
    ///   [[LE::end:T:CKSUM]]
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