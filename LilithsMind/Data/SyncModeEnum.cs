// ============================================================
//  SyncModeEnum — LilithsMind
//  LilithsMind/Network/SyncModeEnum.cs
//
//  Transport mode for the Heart → Soul sync payload delivery.
//  Configured in LilithsHeart.cfg and read by both Heart and Soul.
//
//  ChunkPush  — default. Heart sends payload as tiered chat chunks
//               on client connect. No extra ports or URLs needed.
//
//  HttpServer — Heart starts an HttpListener on a configured port.
//               On connect, Heart sends [[LG:sync-url:<url>]] and
//               Soul fetches the payload directly via HTTP.
//               Server admin must open the configured port.
//
//  StaticUrl  — Admin hosts the payload at a URL (CDN, Gist, etc.)
//               and sets StaticSyncUrl in HeartConfig. On connect,
//               Heart sends [[LG:sync-url:<configured-url>]] and
//               Soul fetches from that URL. Heart hosts nothing.
//
//  For HttpServer and StaticUrl, SyncFallbackToChunks in HeartConfig
//  controls whether a failed fetch falls back to chunk delivery.
// ============================================================

namespace LilithsMind.Data;

public enum SyncModeEnum
{
    /// <summary>
    /// Default. Tiered GZip+Base64 chunks sent via chat messages on connect.
    /// No extra configuration required.
    /// </summary>
    ChunkPush,

    /// <summary>
    /// Heart hosts an HttpListener serving the sync payload.
    /// Soul fetches via HTTP on connect. Requires HttpPort to be
    /// open in the server firewall.
    /// </summary>
    HttpServer,

    /// <summary>
    /// Admin hosts the payload at a static URL.
    /// Heart sends a redirect sentinel on connect, Soul fetches from
    /// StaticSyncUrl. Heart hosts nothing extra.
    /// </summary>
    StaticUrl,
}