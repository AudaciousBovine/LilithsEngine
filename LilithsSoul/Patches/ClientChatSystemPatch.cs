using HarmonyLib;
using ProjectM.Network;
using ProjectM.UI;
using Unity.Collections;
using Unity.Entities;
using LilithsSoul.Foundation;
using LilithsSoul.Network;

// ============================================================
//  ClientChatSystemPatch — LilithsSoul
//
//  Intercepts incoming server messages on the client to extract
//  LilithsEngine sync payload chunks before they reach the UI.
//
//  Hook target: ClientChatSystem.OnUpdate (prefix)
//  ──────────────────────────────────────────────
//  Uses __instance._ReceiveChatMessagesQuery directly — the same
//  query the system itself processes. This is the established
//  pattern from ZUI and Eclipse.
//
//  Prefix (not postfix) so we can destroy consumed entities
//  before ClientChatSystem processes them, preventing chunk
//  text from ever appearing in the chat window.
//
//  [CHANGED] Added ServerChatMessageType.System filter before
//            passing messages to SyncReceiver. Player chat and
//            other message types are skipped immediately, avoiding
//            unnecessary StartsWith checks on every chat message.
//
//  [PERFORMANCE] Per-frame cost is negligible — zero entities
//                in _ReceiveChatMessagesQuery outside of a connect
//                event. The MessageType enum check is a single
//                integer comparison — effectively free.
//                After the type filter, only system messages
//                reach SyncReceiver.TryHandleMessage().
// ============================================================

namespace LilithsSoul.Patches;

[HarmonyPatch(typeof(ClientChatSystem), nameof(ClientChatSystem.OnUpdate))]
internal static class ClientChatSystemPatch
{
    private const string LOG_SOURCE = "LilithsSoul.ClientChatSystemPatch";

    [HarmonyPrefix]
    static void Prefix(ClientChatSystem __instance)
    {
        if (Soul.ClientWorld == null) return;

        var entities = __instance._ReceiveChatMessagesQuery.ToEntityArray(Allocator.Temp);

        try
        {
            foreach (var entity in entities)
            {
                if (!entity.Has<ChatMessageServerEvent>()) continue;

                var chatEvent = entity.Read<ChatMessageServerEvent>();

                // [CHANGED] Skip non-system messages early. Heart sends payload
                //           chunks as ServerChatMessageType.System — player chat,
                //           global messages, and other types are never LG chunks.
                //           This avoids a string allocation and StartsWith check
                //           on every piece of player chat that passes through.
                if (chatEvent.MessageType != ServerChatMessageType.System) continue;

                string message = chatEvent.MessageText.ToString();

                if (SyncReceiver.TryHandleMessage(message))
                {
                    // Consumed — destroy so chunk text never appears in chat UI.
                    Soul.ClientWorld.EntityManager.DestroyEntity(entity);
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }
}