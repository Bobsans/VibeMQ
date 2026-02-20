namespace VibeMQ.Configuration;

/// <summary>
/// Rate limiting configuration for connections and messages.
/// </summary>
public sealed class RateLimitOptions {
    /// <summary>
    /// Whether rate limiting is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of new connections per IP per time window. Default: 20.
    /// </summary>
    public int MaxConnectionsPerIpPerWindow { get; set; } = 20;

    /// <summary>
    /// Maximum messages per client per second. Default: 1000.
    /// </summary>
    public int MaxMessagesPerClientPerSecond { get; set; } = 1000;

    /// <summary>
    /// Time window for connection rate limiting. Default: 60 seconds.
    /// </summary>
    public TimeSpan ConnectionWindow { get; set; } = TimeSpan.FromSeconds(60);
}
