namespace VibeMQ.Configuration;

/// <summary>
/// Top-level configuration options for the VibeMQ broker server.
/// </summary>
public sealed class BrokerOptions {
    /// <summary>
    /// TCP port to listen on. Default: 8080.
    /// </summary>
    public int Port { get; set; } = 8080;

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
    public string? AuthToken { get; set; }

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
}
