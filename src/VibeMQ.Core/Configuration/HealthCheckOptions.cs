namespace VibeMQ.Configuration;

/// <summary>
/// Configuration for the lightweight HTTP health check server.
/// </summary>
public sealed class HealthCheckOptions {
    /// <summary>
    /// Whether the health check endpoint is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// HTTP port for health check requests. Default: 2926 (broker port + 1).
    /// </summary>
    public int Port { get; set; } = 2926;
}
