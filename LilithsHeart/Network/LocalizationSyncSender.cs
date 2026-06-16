// ============================================================
//  LocalizationSyncSender — LilithsHeart
//  LilithsHeart/Network/LocalizationSyncSender.cs
//
//  Sends a localization payload to a specific client that
//  requested a language different from the server default.
//
//  Flow:
//  ──────
//  1. Soul sends [[LE::lang-request:Spanish]] via chat message
//  2. ServerChatSystemPatch routes to HandleLangRequest()
//  3. LocalizationFileService.BuildLocalizationPayload() builds
//     a ServerSyncPayload with only DisplayName + DescriptionText
//  4. Payload is GZip+Base64 chunked and enqueued for the client
//
//  Protocol:
//  ──────────
//  Reuses the existing chunk protocol with tier = Critical
//  (same tier as appearance overrides in the main sync payload).
//  Soul's existing SyncReceiver.HandleEnd() decodes and applies
//  the payload — LocalizationPatcher and DescriptionPatcher
//  overwrite the default-language values with the requested ones.
//
//  Language unavailable:
//  ──────────────────────
//  If the requested language has no configured overrides,
//  sends [[LE::lang-unavailable:<language>]] so Soul can log
//  a warning and stay on the default language.
//
//  [PERFORMANCE] Chunk building runs once per language request
//                per connect — rare event, negligible cost.
// ============================================================

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Unity.Collections;
using Unity.Entities;
using ProjectM;
using ProjectM.Network;
using Il2CppInterop.Runtime;
using LilithsHeart.Config;
using LilithsHeart.Foundation;
using LilithsHeart.Services;
using LilithsMind.Data;

namespace LilithsHeart.Network;

public static class LocalizationSyncSender
{
    private const string LOG_SOURCE        = "LilithsHeart.LocalizationSyncSender";
    private const string UNAVAILABLE_PREFIX = "[[LE::lang-unavailable:";
    private const int    MAX_CHUNK_CONTENT  = 440;

    static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = false };

    // [PERFORMANCE] Static readonly — allocated once.
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
    /// Handles a language request from Soul.
    /// If the language is available, builds and enqueues a localization
    /// payload for this client. If unavailable, sends [[LE::lang-unavailable:X]].
    /// </summary>
    public static void HandleRequest(
        Entity    userEntity,
        Entity    characterEntity,
        int       userIndex,
        string    requestedLanguage)
    {
        var serverIdentity = HeartConfig.ServerName.Value;

        if (!LocalizationFileService.HasLanguage(requestedLanguage))
        {
            HeartLogger.Info(LOG_SOURCE,
                $"Language '{requestedLanguage}' not available — notifying client.");
            SendUnavailable(userEntity, characterEntity, userIndex, requestedLanguage);
            return;
        }

        var payload = LocalizationFileService.BuildLocalizationPayload(
            serverIdentity, requestedLanguage);

        if (payload == null || payload.ItemAppearanceOverrides.Count == 0)
        {
            HeartLogger.Warning(LOG_SOURCE,
                $"Language '{requestedLanguage}' has no overrides — notifying client.");
            SendUnavailable(userEntity, characterEntity, userIndex, requestedLanguage);
            return;
        }

        HeartLogger.Info(LOG_SOURCE,
            $"Sending '{requestedLanguage}' localization payload " +
            $"({payload.ItemAppearanceOverrides.Count} override(s)) to userIndex {userIndex}.");

        EnqueueLocalizationPayload(userEntity, characterEntity, userIndex, payload);
    }

    // ── Internal ─────────────────────────────────────────────

    static void EnqueueLocalizationPayload(
        Entity         userEntity,
        Entity         characterEntity,
        int            userIndex,
        LilithsMind.Network.ServerSyncPayload payload)
    {
        var json      = JsonSerializer.Serialize(payload, _writeOptions);
        var blob      = BuildBlob(json);
        var messages  = BuildMessages(blob).ToList();

        var userNetId      = userEntity.Read<NetworkId>();
        var characterNetId = characterEntity.Read<NetworkId>();

        SyncQueue.Enqueue(
            userEntity, characterEntity,
            userNetId, characterNetId,
            userIndex, messages);

        HeartLogger.Debug(LOG_SOURCE,
            $"Enqueued {messages.Count} localization chunk(s) for userIndex {userIndex}.");
    }

    static void SendUnavailable(
        Entity userEntity,
        Entity characterEntity,
        int    userIndex,
        string languageName)
    {
        var userNetId      = userEntity.Read<NetworkId>();
        var characterNetId = characterEntity.Read<NetworkId>();
        var message        = $"{UNAVAILABLE_PREFIX}{languageName}]]";

        var em = Heart.EntityManager;
        SendMessage(em, userEntity, characterEntity, userNetId, characterNetId, userIndex, message);
    }

    static (string[] Chunks, string Checksum) BuildBlob(string json)
    {
        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using (var gz = new GZipStream(ms, CompressionLevel.Optimal))
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                gz.Write(bytes, 0, bytes.Length);
            }
            compressed = ms.ToArray();
        }

        var encoded  = Convert.ToBase64String(compressed);
        var checksum = ComputeHash(encoded);
        var chunks   = Chunkify(encoded);

        return (chunks, checksum);
    }

    // Reuses Critical tier (0) so SyncReceiver decodes and applies it
    // identically to the main sync payload's appearance slice.
    static IEnumerable<string> BuildMessages((string[] Chunks, string Checksum) blob)
    {
        int t = (int)SyncTierEnum.Critical;
        yield return $"[[LE::begin:{t}:{blob.Chunks.Length}:{blob.Checksum}]]";
        for (int i = 0; i < blob.Chunks.Length; i++)
            yield return $"[[LE::{t}:{i:D4}]]{blob.Chunks[i]}";
        yield return $"[[LE::end:{t}:{blob.Checksum}]]";
    }

    static string[] Chunkify(string input)
    {
        var chunks = new List<string>();
        int pos    = 0;
        while (pos < input.Length)
        {
            int len = Math.Min(MAX_CHUNK_CONTENT, input.Length - pos);
            chunks.Add(input.Substring(pos, len));
            pos += len;
        }
        return chunks.ToArray();
    }

    static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..8];
    }

    static void SendMessage(
        EntityManager em,
        Entity userEntity,
        Entity characterEntity,
        NetworkId userNetId,
        NetworkId characterNetId,
        int userIndex,
        string text)
    {
        if (text.Length > 509) text = text[..509];

        ChatMessageServerEvent chatEvent = new()
        {
            MessageText   = new FixedString512Bytes(text),
            MessageType   = ServerChatMessageType.System,
            FromCharacter = characterNetId,
            FromUser      = userNetId,
            TimeUTC       = DateTime.UtcNow.Ticks,
        };

        var entity = em.CreateEntity(_networkEventComponents);
        entity.Write(new FromCharacter { Character = characterEntity, User = userEntity });
        entity.Write(_networkEventType);
        entity.Write(chatEvent);
        entity.Write(new SendEventToUser { UserIndex = userIndex });
    }
}