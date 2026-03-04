using VibeMQ.Protocol;
using VibeMQ.Protocol.Compression;

namespace VibeMQ.Configuration;

/// <summary>
/// Top-level configuration options for the VibeMQ broker server.
/// </summary>
public sealed class BrokerOptions {
    /// <summary>
    /// TCP port to listen on. Default: 2925 (VibeMQ standard port).
    /// </summary>
    public int Port { get; set; } = 2925;

    /// <summary>
    /// Maximum number of concurrent client connections. Default: 1000.
    /// </summary>
    public int MaxConnections { get; set; } = 1000;

    /// <summary>
    /// Maximum message size in bytes. Default: 1 MB.
    /// </summary>
    public int MaxMessageSize { get; set; } = 1_048_576;

    /// <summary>
    /// Whether token-based authentication is required. Default: false.
    /// </summary>
    public bool EnableAuthentication { get; set; }

    /// <summary>
    /// Token used for simple authentication when <see cref="EnableAuthentication"/> is true.
    /// </summary>
    [Obsolete("Use Authorization with username/password instead. AuthToken is ignored when UseAuthorization() is configured.")]
    public string? AuthToken { get; set; }

    /// <summary>
    /// Authorization configuration for username/password authentication with per-queue ACL.
    /// When set (non-null), username/password auth is active and <see cref="AuthToken"/> is ignored.
    /// </summary>
    public AuthorizationOptions? Authorization { get; set; }

    /// <summary>
    /// Default options applied to newly created queues.
    /// </summary>
    public QueueDefaults QueueDefaults { get; set; } = new();

    /// <summary>
    /// TLS/SSL configuration. Disabled by default.
    /// </summary>
    public TlsOptions Tls { get; set; } = new();

    /// <summary>
    /// Rate limiting configuration.
    /// </summary>
    public RateLimitOptions RateLimit { get; set; } = new();

    /// <summary>
    /// Compression algorithms the broker is willing to use, in descending preference order.
    /// The broker picks the first algorithm that the client also supports.
    /// An empty list disables compression negotiation.
    /// </summary>
    public IReadOnlyList<CompressionAlgorithm> SupportedCompressions { get; set; }
        = [CompressionAlgorithm.Brotli, CompressionAlgorithm.GZip];

    /// <summary>
    /// Minimum serialized body size in bytes required to apply compression.
    /// Bodies smaller than this threshold are sent uncompressed even when an algorithm is negotiated.
    /// Default: <see cref="ProtocolConstants.COMPRESSION_THRESHOLD"/> (1 KB).
    /// </summary>
    public int CompressionThreshold { get; set; } = ProtocolConstants.COMPRESSION_THRESHOLD;
}
