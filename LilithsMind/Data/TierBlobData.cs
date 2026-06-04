using LilithsMind.Data;

// ============================================================
//  TierBlobData — LilithsHeart
//  LilithsHeart/Network/TierBlobData.cs
//
//  Pre-built chunk data for a single sync tier, ready to be
//  enqueued into SyncQueue and sent to a connecting client.
//
//  Built by SyncPayloadCache when the payload is serialized.
//  Consumed by SyncSender.BuildTierMessages() to produce the
//  actual chat message strings.
//
//  Fields:
//  ───────
//  Tier       — priority level, determines send order
//  Chunks     — pre-built base64+gzip compressed chunk strings,
//               each fits within FixedString512Bytes (≤450 chars)
//  ChunkCount — convenience accessor (Chunks.Length)
//  Checksum   — first 8 hex chars of SHA256 over the compressed
//               payload for this tier. Soul verifies on receipt
//               to detect truncated or corrupted transmissions.
//
//  [CHANGED] Added `using LilithsMind.Data;` — SyncTierEnum was
//            relocated from LilithsHeart.Network to LilithsMind.Network
//            so the receiver (Soul) can share the same definition.
//            No logic change; only the type's namespace moved.
//
//  [PERFORMANCE] Immutable after construction — safe to read from
//                multiple threads (SchedulerPatch drain loop).
//                Chunks array is pre-allocated at build time —
//                no allocation occurs during send.
// ============================================================

namespace LilithsMind.Data;

public sealed class TierBlobData
{
    /// <summary>
    /// Priority tier — determines send order (Critical first).
    /// </summary>
    public SyncTierEnum Tier { get; }

    /// <summary>
    /// Pre-built chunk strings, each ≤450 chars.
    /// Each chunk is a base64-encoded segment of the GZip-compressed
    /// tier JSON. SyncSender wraps each in [[LG:T:NNNN]]<chunk>.
    /// </summary>
    public string[] Chunks { get; }

    /// <summary>
    /// Number of chunks — convenience accessor for Chunks.Length.
    /// Used by SyncSender when building begin/end sentinels.
    /// </summary>
    public int ChunkCount => Chunks.Length;

    /// <summary>
    /// First 8 hex chars of SHA256 over the compressed tier payload.
    /// Sent in begin and end sentinels. Soul verifies on receipt.
    /// </summary>
    public string Checksum { get; }

    public TierBlobData(SyncTierEnum tier, string[] chunks, string checksum)
    {
        Tier     = tier;
        Chunks   = chunks;
        Checksum = checksum;
    }
}