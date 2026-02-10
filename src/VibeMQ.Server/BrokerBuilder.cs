using VibeMQ.Core.Configuration;
using VibeMQ.Core.Enums;

namespace VibeMQ.Server;

/// <summary>
/// Fluent builder for configuring and creating a <see cref="BrokerServer"/> instance.
/// </summary>
public sealed class BrokerBuilder {
    private readonly BrokerOptions _options = new();
    private readonly HealthCheckOptions _healthCheckOptions = new();

    private BrokerBuilder() { }

    /// <summary>
    /// Creates a new builder instance.
    /// </summary>
    public static BrokerBuilder Create() => new();

    /// <summary>
    /// Sets the TCP port for the broker to listen on.
    /// </summary>
    public BrokerBuilder UsePort(int port) {
        _options.Port = port;
        return this;
    }

    /// <summary>
    /// Enables authentication with the specified token.
    /// </summary>
    public BrokerBuilder UseAuthentication(string token) {
        _options.EnableAuthentication = true;
        _options.AuthToken = token;
        return this;
    }

    /// <summary>
    /// Configures default queue options.
    /// </summary>
    public BrokerBuilder ConfigureQueues(Action<QueueOptionsFluent> configure) {
        var fluent = new QueueOptionsFluent(_options.QueueDefaults);
        configure(fluent);
        return this;
    }

    /// <summary>
    /// Configures health check options.
    /// </summary>
    public BrokerBuilder ConfigureHealthChecks(Action<HealthCheckOptions> configure) {
        configure(_healthCheckOptions);
        return this;
    }

    /// <summary>
    /// Sets the maximum message size in bytes.
    /// </summary>
    public BrokerBuilder UseMaxMessageSize(int maxMessageSize) {
        _options.MaxMessageSize = maxMessageSize;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of concurrent connections.
    /// </summary>
    public BrokerBuilder UseMaxConnections(int maxConnections) {
        _options.MaxConnections = maxConnections;
        return this;
    }

    /// <summary>
    /// Builds the broker server instance.
    /// </summary>
    public BrokerServer Build() {
        // TODO: Wire up DI, logging, and all services
        throw new NotImplementedException("BrokerBuilder.Build is not yet implemented.");
    }
}

/// <summary>
/// Fluent API for configuring queue defaults.
/// </summary>
public sealed class QueueOptionsFluent {
    private readonly QueueDefaults _defaults;

    internal QueueOptionsFluent(QueueDefaults defaults) {
        _defaults = defaults;
    }

    public DeliveryMode DefaultDeliveryMode {
        get => _defaults.DefaultDeliveryMode;
        set => _defaults.DefaultDeliveryMode = value;
    }

    public int MaxQueueSize {
        get => _defaults.MaxQueueSize;
        set => _defaults.MaxQueueSize = value;
    }

    public bool EnableAutoCreate {
        get => _defaults.EnableAutoCreate;
        set => _defaults.EnableAutoCreate = value;
    }
}
