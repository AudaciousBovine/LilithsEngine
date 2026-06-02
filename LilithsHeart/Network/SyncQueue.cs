using ProjectM.Network;       // NetworkId
using Unity.Entities;
using LilithsHeart.Foundation; // Heart, HeartLogger

// ============================================================
//  SyncQueue — LilithsHeart
//  LilithsHeart/Network/SyncQueue.cs
//
//  Thread-safe queue of pending sync messages to be sent to
//  connecting clients. SchedulerPatch drains this queue at a
//  controlled rate each server frame.
//
//  Why queued instead of immediate?
//  ──────────────────────────────────
//  Sending all chunks immediately on connect creates a spike of
//  ECS entity creations in a single frame. On a busy server with
//  multiple simultaneous connects this could cause frame hitches.
//  SyncQueue decouples enqueueing (connect event) from sending
//  (frame drain) so the cost is spread across frames.
//
//  Structure:
//  ───────────
//  Each pending send is a SyncPendingEntry — a client's routing
//  info plus a queue of message strings to send. Entries are
//  processed in FIFO order. Within each entry, messages are
//  sent in the order they were enqueued (tier order preserved).
//
//  [CHANGED] Stale-entity hardening + drain diagnostics.
//  ──────────────────────────────────────────────────────
//  The queue spans frames, so a client can DISCONNECT between
//  enqueue (connect) and drain. The previous version re-read
//  NetworkId off the stored entities at SEND time — reading a
//  component off a destroyed/recycled entity throws, which would
//  silently abort the drain loop and leave chunks unsent (the
//  exact "enqueued but never received" symptom).
//
//  Two fixes:
//   1. NetworkIds are now captured at ENQUEUE time (entities are
//      definitely valid then) and carried in the entry, so the
//      send path no longer reads off possibly-dead entities.
//   2. Drain() guards each entry with EntityManager.Exists(): if
//      the client's user entity is gone, the whole entry is dropped
//      with a log line rather than attempting a doomed send.
//
//  Drain() now also logs how many chunks it sent and how many
//  entries remain, so a connect can be traced end-to-end in the log.
//
//  Thread safety:
//  ───────────────
//  Enqueue() may be called from the connect event (main thread).
//  Drain() is called from SchedulerPatch (main thread).
//  Both run on the server main thread so a simple lock suffices.
//
//  [PERFORMANCE] Enqueue() is O(n) over messages — called once
//                per connect. Drain() processes at most
//                ChunksPerFrame entities per frame — O(1) amortized.
//                The added Exists() check is one cheap ECS lookup per
//                drained entry, only while a connect is in flight.
// ============================================================

namespace LilithsHeart.Network;

public static class SyncQueue
{
    private const string LOG_SOURCE = "LilithsHeart.SyncQueue";

    // How many chunk entities to create per server frame.
    // Keeps per-frame ECS entity creation bounded.
    // [PERFORMANCE] Tune this if large connects cause frame hitches.
    public const int ChunksPerFrame = 10;

    static readonly object _lock = new();

    // FIFO queue of pending client sends.
    static readonly Queue<SyncPendingEntry> _pending = new();

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Enqueues all messages for a connecting client.
    /// Called once per connect from SyncSender.EnqueueSyncTiers().
    /// Messages are sent in the order they are enqueued — tier
    /// order is preserved by the caller.
    ///
    /// [CHANGED] Now also captures the user/character NetworkIds at
    ///           enqueue time so the drain never re-reads them off a
    ///           potentially-destroyed entity.
    ///
    /// [PERFORMANCE] O(n) over messages — called once per connect.
    /// </summary>
    public static void Enqueue(
        Entity userEntity,
        Entity characterEntity,
        NetworkId userNetId,
        NetworkId characterNetId,
        int    userIndex,
        IEnumerable<string> messages)
    {
        var entry = new SyncPendingEntry(
            userEntity, characterEntity, userNetId, characterNetId, userIndex);

        foreach (var message in messages)
            entry.Messages.Enqueue(message);

        lock (_lock)
            _pending.Enqueue(entry);
    }

    /// <summary>
    /// Drains up to ChunksPerFrame messages from the front of the queue.
    /// Called each server frame by SchedulerPatch.
    /// Sends each message via SyncSender.SendQueuedChunk().
    /// Removes entries when all their messages have been sent.
    ///
    /// [CHANGED] Skips (and drops) entries whose client has disconnected
    ///           since enqueue, and logs drain progress.
    ///
    /// [PERFORMANCE] Creates at most ChunksPerFrame ECS entities per frame.
    ///               O(ChunksPerFrame) — bounded constant cost per frame.
    /// </summary>
    public static void Drain()
    {
        int sent = 0;
        var em   = Heart.EntityManager;

        lock (_lock)
        {
            while (sent < ChunksPerFrame && _pending.Count > 0)
            {
                var entry = _pending.Peek();

                // Client gone since enqueue? Drop the whole entry — sending
                // to a destroyed user entity would throw and abort the drain.
                if (!em.Exists(entry.UserEntity))
                {
                    HeartLogger.Info(LOG_SOURCE,
                        $"Client (userIndex {entry.UserIndex}) disconnected before drain " +
                        $"— dropping {entry.Messages.Count} unsent chunk(s).");
                    _pending.Dequeue();
                    continue;
                }

                if (entry.Messages.Count == 0)
                {
                    _pending.Dequeue();
                    continue;
                }

                var message = entry.Messages.Dequeue();

                SyncSender.SendQueuedChunk(
                    entry.UserEntity,
                    entry.CharacterEntity,
                    entry.UserNetId,
                    entry.CharacterNetId,
                    entry.UserIndex,
                    message);

                sent++;

                // If this entry is exhausted, remove it.
                if (entry.Messages.Count == 0)
                    _pending.Dequeue();
            }
        }

        if (sent > 0)
            HeartLogger.Info(LOG_SOURCE,
                $"Drained {sent} chunk(s); {_pending.Count} entr(ies) still pending.");
    }

    /// <summary>
    /// Returns true if there are pending messages to send.
    /// Used by SchedulerPatch to skip the drain call when idle.
    /// [PERFORMANCE] O(1) — avoids lock acquisition when queue is empty.
    /// </summary>
    public static bool HasPending => _pending.Count > 0;

    /// <summary>
    /// Clears all pending entries.
    /// Called by Heart.OnDestroy() on server shutdown.
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
            _pending.Clear();
    }

    // ── Internal ─────────────────────────────────────────────

    sealed class SyncPendingEntry
    {
        public Entity    UserEntity      { get; }
        public Entity    CharacterEntity { get; }
        public NetworkId UserNetId       { get; }   // captured at enqueue
        public NetworkId CharacterNetId  { get; }   // captured at enqueue
        public int       UserIndex       { get; }

        // Messages remaining to send for this client.
        public Queue<string> Messages { get; } = new();

        public SyncPendingEntry(
            Entity userEntity,
            Entity characterEntity,
            NetworkId userNetId,
            NetworkId characterNetId,
            int userIndex)
        {
            UserEntity      = userEntity;
            CharacterEntity = characterEntity;
            UserNetId       = userNetId;
            CharacterNetId  = characterNetId;
            UserIndex       = userIndex;
        }
    }
}