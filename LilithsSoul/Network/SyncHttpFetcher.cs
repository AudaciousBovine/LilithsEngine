// ============================================================
//  SyncHttpFetcher — LilithsSoul
//  LilithsSoul/Network/SyncHttpFetcher.cs
//
//  Fetches the sync payload from a URL via UnityWebRequest.
//  Used when Soul receives a [[LG:sync-url:...]] redirect sentinel
//  (SyncMode = HttpServer or StaticUrl on the server).
//
//  Fetch flow:
//    1. SyncReceiver.HandleRedirect() calls Fetch() with url,
//       onSuccess, and onFailure callbacks.
//    2. Fetch() starts a coroutine via SoulCoroutineHost.
//    3. On HTTP 200: deserialize JSON → ServerSyncPayload →
//       invoke onSuccess.
//    4. On any failure: invoke onFailure. Caller decides whether
//       to request chunk fallback based on the fallback flag
//       embedded in the sentinel.
//
//  [PERFORMANCE] Single HTTP request per connect — O(1) network
//                round trip vs O(chunks) chat message round trips.
//                Runs on Unity's coroutine system via SoulCoroutineHost.
//                No main thread blocking.
// ============================================================

using System.Text.Json;
using UnityEngine.Networking;
using LilithsMind.Network;
using LilithsSoul.Foundation;

namespace LilithsSoul.Network;

public static class SyncHttpFetcher
{
    private const string LOG_SOURCE = "LilithsSoul.SyncHttpFetcher";

    static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Starts an async HTTP GET to the given URL.
    /// Deserializes the response as ServerSyncPayload and invokes
    /// onSuccess or onFailure on the Unity main thread via coroutine.
    ///
    /// Timeout: 10 seconds — long enough for a cold server response,
    /// short enough not to block the player indefinitely.
    /// </summary>
    public static void Fetch(
        string url,
        Action<ServerSyncPayload> onSuccess,
        Action onFailure)
    {
        // SoulCoroutineHost.Run() is the correct API — no .Instance property exists.
        SoulCoroutineHost.Run(FetchCoroutine(url, onSuccess, onFailure));
    }

    // ── Internal ─────────────────────────────────────────────

    static System.Collections.IEnumerator FetchCoroutine(
        string url,
        Action<ServerSyncPayload> onSuccess,
        Action onFailure)
    {
        SoulLogger.Info(LOG_SOURCE, $"Fetching sync payload from '{url}'...");

        // [CHANGED] UnityWebRequest is not IDisposable in IL2CPP — no using statement.
        var request = UnityWebRequest.Get(url);
        request.timeout = 10;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            SoulLogger.Warning(LOG_SOURCE,
                $"HTTP fetch failed: {request.error} (url: '{url}')");
            onFailure();
            yield break;
        }

        var json = request.downloadHandler.text;

        if (string.IsNullOrWhiteSpace(json))
        {
            SoulLogger.Warning(LOG_SOURCE, "HTTP fetch returned empty response.");
            onFailure();
            yield break;
        }

        ServerSyncPayload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<ServerSyncPayload>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            SoulLogger.Warning(LOG_SOURCE,
                $"Failed to deserialize sync payload: {ex.Message}");
            onFailure();
            yield break;
        }

        if (payload == null)
        {
            SoulLogger.Warning(LOG_SOURCE, "Deserialized payload was null.");
            onFailure();
            yield break;
        }

        SoulLogger.Info(LOG_SOURCE,
            $"HTTP sync fetch succeeded — " +
            $"hash: {payload.PayloadHash}, " +
            $"appearances: {payload.ItemAppearanceOverrides.Count}, " +
            $"recipes: {payload.RecipeOverrides.Count}.");

        onSuccess(payload);
    }
}