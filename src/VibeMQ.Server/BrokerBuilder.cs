using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VibeMQ.Configuration;
using VibeMQ.Enums;
using VibeMQ.Interfaces;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;
using VibeMQ.Server.Delivery;
using VibeMQ.Server.Handlers;
using VibeMQ.Server.Handlers.Admin;
using VibeMQ.Server.Queues;
using VibeMQ.Server.Security;
using VibeMQ.Server.Storage;

namespace VibeMQ.Server;

/// <summary>
/// Fluent builder for configuring and creating a <see cref="BrokerServer"/> instance.
/// </summary>
public sealed class BrokerBuilder {
    private readonly BrokerOptions _options = new();
    private readonly HealthCheckOptions _healthCheckOptions = new();
    private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private Func<ILoggerFactory, IStorageProvider>? _storageProviderFactory;

    private BrokerBuilder() { }

    /// <summary>
    /// Creates a new builder instance.
    /// </summary>
    public static BrokerBuilder Create() => new();

    /// <summary>
    /// Applies configuration from a <see cref="BrokerOptions"/> instance (e.g. from Microsoft.Extensions.Options).
    /// </summary>
    public BrokerBuilder ConfigureFrom(BrokerOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        _options.Port = options.Port;
        _options.MaxConnections = options.MaxConnections;
        _options.MaxMessageSize = options.MaxMessageSize;
        _options.Authorization = options.Authorization;
        _options.QueueDefaults = options.QueueDefaults;
        _options.Tls = options.Tls;
        _options.RateLimit = options.RateLimit;
        _options.SupportedCompressions = options.SupportedCompressions;
        _options.CompressionThreshold = options.CompressionThreshold;
        return this;
    }

    /// <summary>
    /// Sets the TCP port for the broker to listen on.
    /// </summary>
    public BrokerBuilder UsePort(int port) {
        _options.Port = port;
        return this;
    }

    /// <summary>
    /// Enables username/password authorization with per-queue ACL stored in SQLite.
    /// </summary>
    public BrokerBuilder UseAuthorization(Action<AuthorizationOptions> configure) {
        var authOptions = new AuthorizationOptions();
        configure(authOptions);
        _options.Authorization = authOptions;
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
    /// Sets a custom storage provider factory for message persistence.
    /// If not called, <see cref="InMemoryStorageProvider"/> is used by default.
    /// </summary>
    /// <param name="factory">Factory that receives the logger factory and returns a storage provider instance.</param>
    public BrokerBuilder UseStorageProvider(Func<ILoggerFactory, IStorageProvider> factory) {
        _storageProviderFactory = factory;
        return this;
    }

    /// <summary>
    /// Builds the broker server instance with all configured components.
    /// </summary>
    public BrokerServer Build() {
        // Authorization (new username/password mode)
        IPasswordAuthenticationService? passwordAuthService = null;
        IAuthorizationService? authorizationService = null;
        AuthBootstrapper? authBootstrapper = null;

        if (_options.Authorization is not null) {
            var repository = new SqliteAuthRepository(_options.Authorization.DatabasePath);
            var passwordAuth = new PasswordAuthenticationService(repository);
            passwordAuthService = passwordAuth;
            authorizationService = new AuthorizationService();
            authBootstrapper = new AuthBootstrapper(
                repository,
                _options.Authorization,
                _loggerFactory.CreateLogger<AuthBootstrapper>()
            );
        }

        // Metrics
        var metrics = new Metrics.BrokerMetrics();

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

        // Storage provider
        var storageProvider = _storageProviderFactory?.Invoke(_loggerFactory)
            ?? new InMemoryStorageProvider();

        var deadLetterQueue = new DeadLetterQueue(
            storageProvider,
            _loggerFactory.CreateLogger<DeadLetterQueue>()
        );

        // Queue manager
        var queueManager = new QueueManager(
            connectionManager,
            ackTracker,
            deadLetterQueue,
            storageProvider,
            metrics,
            _options.QueueDefaults,
            _loggerFactory.CreateLogger<QueueManager>()
        );

        // Command handlers
        var handlerList = new List<ICommandHandler> {
            new ConnectHandler(_options, passwordAuthService, _loggerFactory.CreateLogger<ConnectHandler>()),
            new PingHandler(),
            new PublishHandler(queueManager, authorizationService, metrics, _loggerFactory.CreateLogger<PublishHandler>()),
            new SubscribeHandler(queueManager, authorizationService, _loggerFactory.CreateLogger<SubscribeHandler>()),
            new UnsubscribeHandler(_loggerFactory.CreateLogger<UnsubscribeHandler>()),
            new AckHandler(queueManager, _loggerFactory.CreateLogger<AckHandler>()),
            new CreateQueueHandler(queueManager, authorizationService, _loggerFactory.CreateLogger<CreateQueueHandler>()),
            new DeleteQueueHandler(queueManager, authorizationService, _loggerFactory.CreateLogger<DeleteQueueHandler>()),
            new QueueInfoHandler(queueManager, authorizationService, _loggerFactory.CreateLogger<QueueInfoHandler>()),
            new ListQueuesHandler(queueManager, authorizationService, _loggerFactory.CreateLogger<ListQueuesHandler>())
        };

        // Register admin handlers only when authorization is enabled
        if (authBootstrapper is not null) {
            // Reuse the same repository instance created above for auth/admin consistency
            var repository = authBootstrapper.Repository;
            handlerList.AddRange([
                new CreateUserHandler(repository, _loggerFactory.CreateLogger<CreateUserHandler>()),
                new DeleteUserHandler(repository, _loggerFactory.CreateLogger<DeleteUserHandler>()),
                new ChangePasswordHandler(repository, _loggerFactory.CreateLogger<ChangePasswordHandler>()),
                new GrantPermissionHandler(repository, _loggerFactory.CreateLogger<GrantPermissionHandler>()),
                new RevokePermissionHandler(repository, _loggerFactory.CreateLogger<RevokePermissionHandler>()),
                new ListUsersHandler(repository, _loggerFactory.CreateLogger<ListUsersHandler>()),
                new GetUserPermissionsHandler(repository, _loggerFactory.CreateLogger<GetUserPermissionsHandler>())
            ]);
        }

        var dispatcher = new CommandDispatcher(handlerList, _loggerFactory.CreateLogger<CommandDispatcher>());

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
            storageProvider,
            metrics,
            authBootstrapper,
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
