namespace VibeMQ.Client.DependencyInjection;

/// <summary>
/// Settings for registering and creating a VibeMQ client via dependency injection.
/// Binds host/port with <see cref="ClientOptions"/> for use with <see cref="IVibeMQClientFactory"/>.
/// </summary>
public sealed class VibeMQClientSettings {
    /// <summary>
    /// Broker host. Default: localhost.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Broker port. Default: 8080.
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Client options (auth, reconnect, keep-alive, TLS, etc.).
    /// </summary>
    public ClientOptions ClientOptions { get; set; } = new();
}
