using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VibeMQ.Core.Configuration;
using VibeMQ.Core.Enums;
using VibeMQ.Core.Interfaces;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;
using VibeMQ.Server.Delivery;
using VibeMQ.Server.Handlers;
using VibeMQ.Core.Metrics;
using VibeMQ.Server.Queues;
using VibeMQ.Server.Security;

namespace VibeMQ.Server;

/// <summary>
/// Fluent builder for configuring and creating a <see cref="BrokerServer"/> instance.
/// </summary>
public sealed class BrokerBuilder {
    private readonly BrokerOptions _options = new();
    private readonly HealthCheckOptions _healthCheckOptions = new();
    private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

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
    /// Configures TLS/SSL options.
    /// </summary>
    public BrokerBuilder UseTls(Action<TlsOptions> configure) {
        configure(_options.Tls);
        _options.Tls.Enabled = true;
        return this;
    }

    /// <summary>
    /// Configures rate limiting options.
    /// </summary>
    public BrokerBuilder ConfigureRateLimiting(Action<RateLimitOptions> configure) {
        configure(_options.RateLimit);
        return this;
    }

    /// <summary>
    /// Sets the logger factory for the broker. If not called, logging is disabled.
    /// </summary>
    public BrokerBuilder UseLoggerFactory(ILoggerFactory loggerFactory) {
        _loggerFactory = loggerFactory;
        return this;
    }

    /// <summary>
    /// Builds the broker server instance with all configured components.
    /// </summary>
    public BrokerServer Build() {
        // Authentication
        IAuthenticationService? authService = null;
        if (_options.EnableAuthentication && !string.IsNullOrEmpty(_options.AuthToken)) {
            authService = new TokenAuthenticationService(_options.AuthToken);
        }

        // Metrics
        var metrics = new Server.Metrics.BrokerMetrics();

        // Connection manager
        var connectionManager = new ConnectionManager(
            _options.MaxConnections,
            metrics,
            _loggerFactory.CreateLogger<ConnectionManager>()
        );

        // Delivery infrastructure
        var ackTracker = new AckTracker(
            logger: _loggerFactory.CreateLogger<AckTracker>()
        );

        var deadLetterQueue = new DeadLetterQueue(
            _loggerFactory.CreateLogger<DeadLetterQueue>()
        );

        // Queue manager
        var queueManager = new QueueManager(
            connectionManager,
            ackTracker,
            deadLetterQueue,
            metrics,
            _options.QueueDefaults,
            _loggerFactory.CreateLogger<QueueManager>()
        );

        // Command handlers
        var handlers = new ICommandHandler[] {
            new ConnectHandler(_options, authService, _loggerFactory.CreateLogger<ConnectHandler>()),
            new PingHandler(),
            new PublishHandler(queueManager, _loggerFactory.CreateLogger<PublishHandler>()),
            new SubscribeHandler(queueManager, _loggerFactory.CreateLogger<SubscribeHandler>()),
            new UnsubscribeHandler(_loggerFactory.CreateLogger<UnsubscribeHandler>()),
            new AckHandler(queueManager, _loggerFactory.CreateLogger<AckHandler>()),
        };

        var dispatcher = new CommandDispatcher(handlers, _loggerFactory.CreateLogger<CommandDispatcher>());

        // Security
        var rateLimiter = new RateLimiter(
            _options.RateLimit,
            _loggerFactory.CreateLogger<RateLimiter>()
        );

        return new BrokerServer(
            _options,
            connectionManager,
            dispatcher,
            queueManager,
            ackTracker,
            rateLimiter,
            metrics,
            _loggerFactory.CreateLogger<BrokerServer>()
        );
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
