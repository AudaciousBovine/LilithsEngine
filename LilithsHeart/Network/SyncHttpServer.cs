// ============================================================
//  SyncHttpServer — LilithsHeart
//  LilithsHeart/Network/SyncHttpServer.cs
//
//  Minimal HTTP server that serves the current sync payload
//  as a JSON endpoint. Used when SyncMode = HttpServer.
//
//  Endpoint:
//    GET http://<serverIp>:<HttpPort>/sync
//    Response: 200 OK, Content-Type: application/json
//    Body: full serialized ServerSyncPayload JSON
//
//  Security note:
//    This endpoint serves read-only mod configuration data only.
//    No player credentials or sensitive data are served.
//    Server admins must open HttpPort in their firewall.
//    Disabled by default — only starts when SyncMode = HttpServer.
//
//  Lifecycle:
//    Start() called from Heart.OnInitialize() when mode is HttpServer.
//    Stop() called from Heart.OnDestroy().
//    The listener runs on a background thread — all endpoint
//    serving happens off the main server thread.
//
//  [PERFORMANCE] O(1) server work per client connect — one HTTP
//                response regardless of payload size or client count.
//                Background thread — no main thread impact.
//                Payload JSON is pre-serialized on each Rebuild()
//                and cached; the endpoint just writes the cached string.
// ============================================================

using System.Net;
using System.Text;
using System.Text.Json;
using LilithsHeart.Config;
using LilithsHeart.Foundation;
using LilithsMind.Data;
using LilithsMind.Network;

namespace LilithsHeart.Network;

public static class SyncHttpServer
{
    private const string LOG_SOURCE = "LilithsHeart.SyncHttpServer";

    static HttpListener?  _listener;
    static Thread?        _thread;
    static volatile bool  _running;

    // Cached serialized payload — updated on each SyncPayloadCache.Rebuild().
    static volatile string? _cachedPayloadJson;

    static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = false };

    // ── Public API ───────────────────────────────────────────

    /// <summary>
    /// Starts the HTTP listener on HeartConfig.HttpPort.
    /// Called from Heart.OnInitialize() when SyncMode = HttpServer.
    /// No-op if already running.
    /// </summary>
    public static void Start()
    {
        if (_running)
        {
            HeartLogger.Warning(LOG_SOURCE, "SyncHttpServer already running.");
            return;
        }

        int port = HeartConfig.HttpPort;

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:{port}/sync/");
            _listener.Start();
            _running = true;

            _thread = new Thread(ServeLoop)
            {
                IsBackground = true,
                Name         = "LilithsGarden.SyncHttpServer",
            };
            _thread.Start();

            HeartLogger.Info(LOG_SOURCE,
                $"SyncHttpServer started on port {port}. " +
                "Ensure this port is open in your server firewall.");
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE,
                $"Failed to start SyncHttpServer on port {port}: {ex.Message}. " +
                "Check that the port is available and the process has permission.");
            _running = false;
        }
    }

    /// <summary>
    /// Stops the HTTP listener.
    /// Called from Heart.OnDestroy().
    /// </summary>
    public static void Stop()
    {
        _running = false;

        try { _listener?.Stop(); }
        catch { /* suppress — listener may already be stopped */ }

        _listener = null;
        _thread   = null;
        HeartLogger.Info(LOG_SOURCE, "SyncHttpServer stopped.");
    }

    /// <summary>
    /// Updates the cached payload JSON served by the endpoint.
    /// Called by SyncPayloadCache after each Rebuild().
    /// Thread-safe — volatile write.
    /// </summary>
    public static void UpdatePayload(ServerSyncPayload payload)
    {
        try
        {
            _cachedPayloadJson = JsonSerializer.Serialize(payload, _writeOptions);
        }
        catch (Exception ex)
        {
            HeartLogger.Error(LOG_SOURCE,
                $"Failed to serialize payload for HTTP endpoint: {ex.Message}");
        }
    }

    // ── Serve loop ────────────────────────────────────────────

    /// <summary>
    /// Background thread loop — blocks on GetContext() waiting for requests.
    /// Each request is handled inline (payload is small, response is fast).
    ///
    /// [PERFORMANCE] Blocking GetContext() — no CPU burn while idle.
    ///               Response is a single cached string write — negligible cost.
    /// </summary>
    static void ServeLoop()
    {
        while (_running)
        {
            try
            {
                var context  = _listener!.GetContext();
                var request  = context.Request;
                var response = context.Response;

                if (request.HttpMethod != "GET")
                {
                    response.StatusCode = 405; // Method Not Allowed
                    response.Close();
                    continue;
                }

                var json = _cachedPayloadJson;

                if (string.IsNullOrEmpty(json))
                {
                    // Payload not yet built — return 503.
                    response.StatusCode = 503;
                    var notReady = Encoding.UTF8.GetBytes("{\"error\":\"Payload not ready\"}");
                    response.ContentType   = "application/json";
                    response.ContentLength64 = notReady.Length;
                    response.OutputStream.Write(notReady, 0, notReady.Length);
                    response.Close();
                    continue;
                }

                var bytes = Encoding.UTF8.GetBytes(json);
                response.StatusCode      = 200;
                response.ContentType     = "application/json";
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
                response.Close();

                HeartLogger.Debug(LOG_SOURCE,
                    $"Served sync payload ({bytes.Length} bytes) to " +
                    $"{request.RemoteEndPoint}.");
            }
            catch (HttpListenerException) when (!_running)
            {
                // Listener stopped — exit cleanly.
                break;
            }
            catch (Exception ex)
            {
                if (_running)
                    HeartLogger.Warning(LOG_SOURCE, $"ServeLoop error: {ex.Message}");
            }
        }
    }
}