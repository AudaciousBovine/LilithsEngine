// ============================================================
//  SyncPayloadCache — LilithsHeart
//  LilithsHeart/Network/SyncPayloadCache.cs
//
//  Builds and caches the ServerSyncPayload split into priority
//  tiers. Each tier is GZip-compressed, base64-encoded, and
//  chunked into FixedString512Bytes-safe strings.
//
//  Heart calls Rebuild() twice:
//    1. Before OnInitialized fires — baseline empty payload.
//    2. After all modules have registered overrides — final payload.
//
//  SyncSender reads GetAllTierBlobs() on client connect and
//  enqueues the blobs into SyncQueue for controlled delivery.
//
//  Tier assignment:
//  ─────────────────
//  Critical   — ItemAppearanceOverrides (names, tooltips, icons)
//  High       — RecipeOverrides + StationRecipeOverrides
//  Normal     — PlayerRecipesToAdd + PlayerRecipesToRemove
//  Low        — Reserved for future modules (Machinations, Grimoire)
//  Background — Reserved for large data sets (Menagerie, Bounty)
//
//  Why GZip + base64?
//  ───────────────────
//  ChatMessageServerEvent.MessageText is FixedString512Bytes.
//  GZip reduces payload size significantly (JSON compresses well
//  at 60-80%). Base64 encodes binary to safe ASCII for the
//  FixedString. Combined this lets us send much larger payloads
//  with fewer chunks than plain JSON.
//
//  [CHANGED] ItemAppearanceConfig → LilithItemConfig.
//            ItemAppearanceData   → LilithItemData.
//            StackSize is on LilithItemData but is server-only —
//            the appearance payload only carries DisplayName,
//            DescriptionText, and Icon to Soul. StackSize is
//            excluded by building a filtered dictionary that
//            only contains entries with at least one appearance
//            field set.
//
//  [PERFORMANCE] Compression and serialization run at most twice
//                at startup. No per-frame or per-connect cost.
//                GetAllTierBlobs() returns cached array — O(1).
// ============================================================

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LilithsHeart.Config;
using LilithsHeart.Foundation;
using LilithsMind.Data;
using LilithsMind.Network;

namespace LilithsHeart.Network;

public static class SyncPayloadCache
{
    private const string LOG_SOURCE = "LilithsHeart.SyncPayloadCache";

    private const int MAX_CHUNK_CONTENT = 440;

    static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = false };

    static volatile TierBlobData[]? _tierBlobs;

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Returns all cached tier blobs, or an empty array if not yet built.
    /// Called by SyncSender.EnqueueSyncTiers() on client connect.
    /// [PERFORMANCE] Returns cached array — O(1), no allocation.
    /// </summary>
    public static IReadOnlyList<TierBlobData> GetAllTierBlobs()
        => _tierBlobs ?? Array.Empty<TierBlobData>();

    /// <summary>
    /// Builds and caches all tier blobs from the current server state.
    /// Called by Heart.OnInitialize() before and after module registration.
    /// [PERFORMANCE] GZip + serialize runs once per call — at most twice
    ///               at startup.
    /// </summary>
    public static void Rebuild(
        string serverIdentity,
        Dictionary<string, LilithRecipeData>  recipeOverrides,
        Dictionary<string, LilithStationData> stationRecipeOverrides,
        List<string>                           playerRecipesToAdd,
        List<string>                           playerRecipesToRemove)
    {
        try
        {
            var identity = SanitizeFolderName(serverIdentity);

            // ── Critical tier — item appearance ──────────────────────────────
            // [CHANGED] LilithItemConfig.Overrides replaces ItemAppearanceConfig.Overrides.
            //           StackSize lives on LilithItemData but is server-only — build a
            //           filtered dictionary containing only entries with at least one
            //           appearance field so Soul never receives StackSize values.
            var appearanceOverrides = LilithItemConfig.Overrides
                .Where(kvp =>
                    kvp.Value.DisplayName     is not null ||
                    kvp.Value.DescriptionText is not null ||
                    kvp.Value.Icon            is not null)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => new LilithItemData
                    {
                        DisplayName     = kvp.Value.DisplayName,
                        DescriptionText = kvp.Value.DescriptionText,
                        Icon            = kvp.Value.Icon,
                        // StackSize intentionally omitted — server-only field.
                    });

            var fullPayload = new ServerSyncPayload
            {
                ServerIdentity          = identity,
                ItemAppearanceOverrides = appearanceOverrides,
            };

            foreach (var (k, v) in recipeOverrides)
                fullPayload.RecipeOverrides[k] = v;
            foreach (var (k, v) in stationRecipeOverrides)
                fullPayload.StationRecipeOverrides[k] = v;

            fullPayload.PlayerRecipesToAdd    = new List<string>(playerRecipesToAdd);
            fullPayload.PlayerRecipesToRemove = new List<string>(playerRecipesToRemove);

            var hashJson    = JsonSerializer.Serialize(fullPayload, _writeOptions);
            var payloadHash = ComputeHash(hashJson);

            fullPayload.PayloadHash = payloadHash;

            // Build one blob per active tier.
            var blobs = new List<TierBlobData>();

            // Critical — always built even if empty so Soul gets the
            // identity + hash on first chunk.
            blobs.Add(BuildBlob(SyncTierEnum.Critical,
                JsonSerializer.Serialize(new
                {
                    fullPayload.ServerIdentity,
                    fullPayload.PayloadHash,
                    fullPayload.ItemAppearanceOverrides,
                }, _writeOptions)));

            // High — only if there are recipe/station overrides.
            if (recipeOverrides.Count > 0 || stationRecipeOverrides.Count > 0)
            {
                blobs.Add(BuildBlob(SyncTierEnum.High,
                    JsonSerializer.Serialize(new
                    {
                        fullPayload.ServerIdentity,
                        fullPayload.PayloadHash,
                        fullPayload.RecipeOverrides,
                        fullPayload.StationRecipeOverrides,
                    }, _writeOptions)));
            }

            // Normal — only if there are player recipe changes.
            if (playerRecipesToAdd.Count > 0 || playerRecipesToRemove.Count > 0)
            {
                blobs.Add(BuildBlob(SyncTierEnum.Normal,
                    JsonSerializer.Serialize(new
                    {
                        fullPayload.ServerIdentity,
                        fullPayload.PayloadHash,
                        fullPayload.PlayerRecipesToAdd,
                        fullPayload.PlayerRecipesToRemove,
                    }, _writeOptions)));
            }

            _tierBlobs = blobs.ToArray();

            int totalChunks = _tierBlobs.Sum(b => b.ChunkCount);

            HeartLogger.Info(LOG_SOURCE,
                $"Payload cached — {_tierBlobs.Length} tier(s), " +
                $"{totalChunks} total chunk(s), hash {payloadHash}, " +
                $"{fullPayload.ItemAppearanceOverrides.Count} appearance override(s), " +
                $"{fullPayload.RecipeOverrides.Count} recipe override(s), " +
                $"{fullPayload.StationRecipeOverrides.Count} station override(s), " +
                $"{fullPayload.PlayerRecipesToAdd.Count} player add(s), " +
                $"{fullPayload.PlayerRecipesToRemove.Count} player remove(s).");
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE, $"Rebuild failed: {ex.Message}");
            _tierBlobs = null;
        }
    }

    // ── Internal ─────────────────────────────────────────────

    static TierBlobData BuildBlob(SyncTierEnum tier, string json)
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
        var chunks   = Chunkify(encoded);
        var checksum = ComputeHash(encoded);

        return new TierBlobData(tier, chunks, checksum);
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

    static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
    }

    static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..8];
    }
}