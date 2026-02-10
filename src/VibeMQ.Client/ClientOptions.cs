namespace VibeMQ.Client;

/// <summary>
/// Configuration options for the VibeMQ client connection.
/// </summary>
public sealed class ClientOptions {
    /// <summary>
    /// Authentication token. If null, no authentication is performed.
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// Reconnection policy for handling connection drops.
    /// </summary>
    public ReconnectPolicy ReconnectPolicy { get; set; } = new();

    /// <summary>
    /// Interval between keep-alive pings. Default: 30 seconds.
    /// </summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for waiting a response from the broker. Default: 10 seconds.
    /// </summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Whether to use TLS for the connection. Default: false.
    /// </summary>
    public bool UseTls { get; set; }

    /// <summary>
    /// Whether to skip TLS certificate validation (for self-signed certs). Default: false.
    /// </summary>
    public bool SkipCertificateValidation { get; set; }
}
