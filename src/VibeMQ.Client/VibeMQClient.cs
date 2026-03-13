using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VibeMQ.Client.Exceptions;
using VibeMQ.Configuration;
using VibeMQ.Interfaces;
using VibeMQ.Models;
using VibeMQ.Enums;
using VibeMQ.Protocol;
using VibeMQ.Protocol.Compression;
using VibeMQ.Protocol.Framing;

namespace VibeMQ.Client;

/// <summary>
/// Client for connecting to a VibeMQ message broker.
/// Handles connection, publish/subscribe, keep-alive, reconnection, and graceful disconnect.
/// </summary>
public sealed partial class VibeMQClient : IVibeMQClient, IAsyncDisposable {
    private readonly ClientOptions _options;
    private readonly ILogger<VibeMQClient> _logger;
    private readonly IServiceProvider? _serviceProvider;
    private readonly ConcurrentDictionary<string, SubscriptionRegistration> _subscriptionHandlers = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtocolMessage>> _pendingResponses = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();

    private readonly FrameWriter _frameWriter = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private string _host = string.Empty;
    private int _port;
    private TcpClient? _tcpClient;
    private Stream? _stream;
    private CancellationTokenSource? _cts;
    private volatile bool _disposed;
    private volatile bool _isConnected;
    private int _reconnecting;

    private VibeMQClient(ClientOptions options, ILogger<VibeMQClient>? logger, IServiceProvider? serviceProvider = null) {
        _options = options;
        _logger = logger ?? NullLogger<VibeMQClient>.Instance;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Whether the client is currently connected to the broker.
    /// </summary>
    public bool IsConnected => _isConnected && _tcpClient?.Connected == true;

    /// <summary>
    /// Connects to a VibeMQ broker using a connection string (URL or key=value format).
    /// </summary>
    /// <param name="connectionString">Connection string, e.g. <c>vibemq://host:2925</c> or <c>Host=localhost;Port=2925</c>.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Connected client.</returns>
    /// <exception cref="VibeMQConnectionStringException">The connection string is invalid.</exception>
    public static async Task<VibeMQClient> ConnectAsync(
        string connectionString,
        ILogger<VibeMQClient>? logger = null,
        CancellationToken cancellationToken = default
    ) {
        return await ConnectAsync(connectionString, logger, serviceProvider: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connects to a VibeMQ broker using a connection string with DI support.
    /// </summary>
    /// <param name="connectionString">Connection string, e.g. <c>vibemq://host:2925</c> or <c>Host=localhost;Port=2925</c>.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="serviceProvider">Optional service provider for resolving message handlers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Connected client.</returns>
    /// <exception cref="VibeMQConnectionStringException">The connection string is invalid.</exception>
    public static async Task<VibeMQClient> ConnectAsync(
        string connectionString,
        ILogger<VibeMQClient>? logger,
        IServiceProvider? serviceProvider,
        CancellationToken cancellationToken
    ) {
        var parsed = VibeMQConnectionString.Parse(connectionString);
        return await ConnectAsync(parsed.Host, parsed.Port, parsed.Options, logger, serviceProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connects to a VibeMQ broker and returns an initialized client.
    /// </summary>
    public static async Task<VibeMQClient> ConnectAsync(
        string host,
        int port,
        ClientOptions? options = null,
        ILogger<VibeMQClient>? logger = null,
        CancellationToken cancellationToken = default
    ) {
        return await ConnectAsync(host, port, options, logger, serviceProvider: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connects to a VibeMQ broker and returns an initialized client with DI support.
    /// </summary>
    public static async Task<VibeMQClient> ConnectAsync(
        string host,
        int port,
        ClientOptions? options,
        ILogger<VibeMQClient>? logger,
        IServiceProvider? serviceProvider,
        CancellationToken cancellationToken
    ) {
        var opts = options ?? new ClientOptions();

        // Pre-flight: validate declarations before attempting connection
        opts.ValidateDeclarations();

        var client = new VibeMQClient(opts, logger, serviceProvider);
        client._host = host;
        client._port = port;

        try {
            await client.ConnectInternalAsync(cancellationToken).ConfigureAwait(false);
        } catch {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return client;
    }

    /// <summary>
    /// Publishes a message to the specified queue.
    /// </summary>
    public async Task PublishAsync<T>(string queueName, T payload, CancellationToken cancellationToken = default) {
        await PublishAsync(queueName, payload, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Publishes a message to the specified queue with custom headers.
    /// </summary>
    public async Task PublishAsync<T>(string queueName, T payload, Dictionary<string, string>? headers, CancellationToken cancellationToken = default) {
        var jsonPayload = JsonSerializer.SerializeToElement(payload, ProtocolSerializer.Options);

        var message = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = queueName,
            Payload = jsonPayload,
            Headers = headers,
        };

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);

        if (response.Type == CommandType.Error) {
            throw new InvalidOperationException($"Publish failed: {response.ErrorMessage}");
        }
    }

    /// <summary>
    /// Subscribes to a queue and invokes the handler for each received message.
    /// Returns a disposable that unsubscribes when disposed.
    /// </summary>
    public async Task<IAsyncDisposable> SubscribeAsync<T>(
        string queueName,
        Func<T, Task> handler,
        CancellationToken cancellationToken = default
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(handler);

        var registration = new SubscriptionRegistration(queueName, _ => ValueTask.CompletedTask);
        registration.SetHandler(async (protocolMessage, deliveryCancellationToken) => {
            if (protocolMessage.Payload.HasValue) {
                var payload = protocolMessage.Payload.Value.Deserialize<T>(ProtocolSerializer.Options);

                if (payload is not null) {
                    await handler(payload).ConfigureAwait(false);
                }
            }

            // Send ACK
            await SendMessageAsync(new ProtocolMessage {
                Id = protocolMessage.Id,
                Type = CommandType.Ack,
            }, deliveryCancellationToken).ConfigureAwait(false);
        });

        if (!_subscriptionHandlers.TryAdd(queueName, registration)) {
            await registration.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Queue '{queueName}' is already subscribed on this client.");
        }

        // Send Subscribe command
        var message = new ProtocolMessage {
            Type = CommandType.Subscribe,
            Queue = queueName,
        };

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);

        if (response.Type == CommandType.Error) {
            if (_subscriptionHandlers.TryRemove(queueName, out var failedRegistration)) {
                await failedRegistration.DisposeAsync().ConfigureAwait(false);
            }

            throw new InvalidOperationException($"Subscribe failed: {response.ErrorMessage}");
        }

        LogSubscribed(queueName);
        return new Subscription(this, queueName, registration.SubscriptionId);
    }

    /// <summary>
    /// Subscribes to a queue using a class-based message handler.
    /// Returns a disposable that unsubscribes when disposed.
    /// </summary>
    public async Task<IAsyncDisposable> SubscribeAsync<TMessage, THandler>(
        string queueName,
        CancellationToken cancellationToken = default
    ) where THandler : IMessageHandler<TMessage> {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var registration = CreateClassBasedRegistration<TMessage, THandler>(queueName);
        if (!_subscriptionHandlers.TryAdd(queueName, registration)) {
            await registration.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Queue '{queueName}' is already subscribed on this client.");
        }

        // Send Subscribe command
        var message = new ProtocolMessage {
            Type = CommandType.Subscribe,
            Queue = queueName,
        };

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);

        if (response.Type == CommandType.Error) {
            if (_subscriptionHandlers.TryRemove(queueName, out var failedRegistration)) {
                await failedRegistration.DisposeAsync().ConfigureAwait(false);
            }

            throw new InvalidOperationException($"Subscribe failed: {response.ErrorMessage}");
        }

        LogSubscribed(queueName);
        return new Subscription(this, queueName, registration.SubscriptionId);
    }

    private SubscriptionRegistration CreateClassBasedRegistration<TMessage, THandler>(string queueName)
        where THandler : IMessageHandler<TMessage> {
        if (_serviceProvider is null) {
            THandler handlerInstance;
            try {
                handlerInstance = Activator.CreateInstance<THandler>();
            } catch (Exception ex) {
                throw new InvalidOperationException(
                    $"Failed to create instance of {typeof(THandler).Name}. Ensure it has a parameterless constructor or is registered in DI.",
                    ex
                );
            }

            var registration = new SubscriptionRegistration(queueName, _ => DisposeResourceAsync(handlerInstance));
            registration.SetHandler(async (protocolMessage, deliveryCancellationToken) => {
                if (protocolMessage.Payload.HasValue) {
                    var payload = protocolMessage.Payload.Value.Deserialize<TMessage>(ProtocolSerializer.Options);
                    if (payload is not null) {
                        await handlerInstance.HandleAsync(payload, deliveryCancellationToken).ConfigureAwait(false);
                    }
                }

                await SendMessageAsync(new ProtocolMessage {
                    Id = protocolMessage.Id,
                    Type = CommandType.Ack,
                }, deliveryCancellationToken).ConfigureAwait(false);
            });
            return registration;
        }

        try {
            using var validationScope = _serviceProvider.CreateScope();
            _ = ActivatorUtilities.GetServiceOrCreateInstance<THandler>(validationScope.ServiceProvider);
        } catch (Exception ex) {
            throw new InvalidOperationException(
                $"Failed to create instance of {typeof(THandler).Name}. Ensure it has a parameterless constructor or is registered in DI.",
                ex
            );
        }

        var scopedRegistration = new SubscriptionRegistration(queueName, static _ => ValueTask.CompletedTask);
        scopedRegistration.SetHandler(async (protocolMessage, deliveryCancellationToken) => {
            if (protocolMessage.Payload.HasValue) {
                var payload = protocolMessage.Payload.Value.Deserialize<TMessage>(ProtocolSerializer.Options);
                if (payload is not null) {
                    await using var scope = _serviceProvider.CreateAsyncScope();
                    var scopedHandler = ActivatorUtilities.GetServiceOrCreateInstance<THandler>(scope.ServiceProvider);
                    await scopedHandler.HandleAsync(payload, deliveryCancellationToken).ConfigureAwait(false);
                }
            }

            await SendMessageAsync(new ProtocolMessage {
                Id = protocolMessage.Id,
                Type = CommandType.Ack,
            }, deliveryCancellationToken).ConfigureAwait(false);
        });
        return scopedRegistration;
    }

    /// <summary>
    /// Unsubscribes from a queue.
    /// </summary>
    public async Task UnsubscribeAsync(string queueName, CancellationToken cancellationToken = default) {
        await UnsubscribeAsync(queueName, expectedSubscriptionId: null, cancellationToken).ConfigureAwait(false);
    }

    internal Task UnsubscribeAsync(string queueName, Guid expectedSubscriptionId, CancellationToken cancellationToken = default) {
        return UnsubscribeAsync(queueName, (Guid?)expectedSubscriptionId, cancellationToken);
    }

    private async Task UnsubscribeAsync(
        string queueName,
        Guid? expectedSubscriptionId,
        CancellationToken cancellationToken = default
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        if (expectedSubscriptionId is { } subscriptionId &&
            _subscriptionHandlers.TryGetValue(queueName, out var existingRegistration) &&
            existingRegistration.SubscriptionId != subscriptionId) {
            return;
        }

        if (_subscriptionHandlers.TryRemove(queueName, out var removedRegistration)) {
            await removedRegistration.DisposeAsync().ConfigureAwait(false);
        }

        if (!IsConnected) {
            return;
        }

        var message = new ProtocolMessage {
            Type = CommandType.Unsubscribe,
            Queue = queueName,
        };

        await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);
        LogUnsubscribed(queueName);
    }

    private static ValueTask DisposeResourceAsync(object? resource) {
        return resource switch {
            IAsyncDisposable asyncDisposable => asyncDisposable.DisposeAsync(),
            IDisposable disposable => DisposeSync(disposable),
            _ => ValueTask.CompletedTask,
        };
    }

    private static ValueTask DisposeSync(IDisposable disposable) {
        disposable.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Creates a queue with the specified name and options.
    /// </summary>
    public async Task CreateQueueAsync(string queueName, QueueOptions? options = null, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(queueName)) {
            throw new ArgumentException("Queue name cannot be null or empty.", nameof(queueName));
        }

        var message = new ProtocolMessage {
            Type = CommandType.CreateQueue,
            Queue = queueName,
        };

        // Serialize options to payload if provided
        if (options is not null) {
            message.Payload = JsonSerializer.SerializeToElement(options, ProtocolSerializer.Options);
        }

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);

        if (response.Type == CommandType.Error) {
            throw new InvalidOperationException($"CreateQueue failed: {response.ErrorMessage}");
        }

        LogQueueCreated(queueName);
    }

    /// <summary>
    /// Deletes a queue by name.
    /// </summary>
    public async Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var message = new ProtocolMessage {
            Type = CommandType.DeleteQueue,
            Queue = queueName,
        };

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);

        if (response.Type == CommandType.Error) {
            throw new InvalidOperationException($"DeleteQueue failed: {response.ErrorMessage}");
        }

        LogQueueDeleted(queueName);
    }

    /// <summary>
    /// Returns metadata about a specific queue, or null if the queue does not exist.
    /// </summary>
    public async Task<QueueInfo?> GetQueueInfoAsync(string queueName, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var message = new ProtocolMessage {
            Type = CommandType.QueueInfo,
            Queue = queueName,
        };

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);

        if (response.Type == CommandType.Error) {
            throw new InvalidOperationException($"GetQueueInfo failed: {response.ErrorMessage}");
        }

        if (!response.Payload.HasValue) {
            return null;
        }

        return response.Payload.Value.Deserialize<QueueInfo>(ProtocolSerializer.Options);
    }

    /// <summary>
    /// Lists all queue names on the broker.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListQueuesAsync(CancellationToken cancellationToken = default) {
        var message = new ProtocolMessage {
            Type = CommandType.ListQueues,
        };

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);

        if (response.Type == CommandType.Error) {
            throw new InvalidOperationException($"ListQueues failed: {response.ErrorMessage}");
        }

        if (!response.Payload.HasValue) {
            return [];
        }

        return response.Payload.Value.Deserialize<List<string>>(ProtocolSerializer.Options) ?? [];
    }

    // ─── Admin (superuser) ───────────────────────────────────────────────────

    /// <summary>
    /// Creates a new user. Superuser-only.
    /// </summary>
    public async Task CreateUserAsync(string username, string password, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrEmpty(password);

        var message = new ProtocolMessage {
            Type = CommandType.AdminCreateUser,
            Payload = JsonSerializer.SerializeToElement(new { username, password }, ProtocolSerializer.Options),
        };

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.Type == CommandType.Error) {
            throw new InvalidOperationException($"CreateUser failed: {response.ErrorMessage}");
        }
    }

    /// <summary>
    /// Deletes a user. Superuser-only. Cannot delete another superuser.
    /// </summary>
    public async Task DeleteUserAsync(string username, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var message = new ProtocolMessage {
            Type = CommandType.AdminDeleteUser,
            Payload = JsonSerializer.SerializeToElement(new { username }, ProtocolSerializer.Options),
        };

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.Type == CommandType.Error) {
            throw new InvalidOperationException($"DeleteUser failed: {response.ErrorMessage}");
        }
    }

    /// <summary>
    /// Changes a user's password. Superuser can change any user; regular users can only change their own.
    /// </summary>
    public async Task ChangePasswordAsync(string username, string newPassword, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrEmpty(newPassword);

        var message = new ProtocolMessage {
            Type = CommandType.AdminChangePassword,
            Payload = JsonSerializer.SerializeToElement(new { username, newPassword }, ProtocolSerializer.Options),
        };

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.Type == CommandType.Error) {
            throw new InvalidOperationException($"ChangePassword failed: {response.ErrorMessage}");
        }
    }

    /// <summary>
    /// Grants permissions on a queue pattern to a user. Superuser-only.
    /// </summary>
    public async Task GrantPermissionAsync(string username, string queuePattern, QueueOperation[] operations, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(queuePattern);
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Length == 0) {
            throw new ArgumentException("At least one operation is required.", nameof(operations));
        }

        var opsStrings = operations.Select(o => o.ToString()).ToArray();
        var message = new ProtocolMessage {
            Type = CommandType.AdminGrantPermission,
            Payload = JsonSerializer.SerializeToElement(new { username, queuePattern, operations = opsStrings }, ProtocolSerializer.Options),
        };

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.Type == CommandType.Error) {
            throw new InvalidOperationException($"GrantPermission failed: {response.ErrorMessage}");
        }
    }

    /// <summary>
    /// Revokes a user's permission on a queue pattern. Superuser-only.
    /// </summary>
    public async Task RevokePermissionAsync(string username, string queuePattern, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(queuePattern);

        var message = new ProtocolMessage {
            Type = CommandType.AdminRevokePermission,
            Payload = JsonSerializer.SerializeToElement(new { username, queuePattern }, ProtocolSerializer.Options),
        };

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.Type == CommandType.Error) {
            throw new InvalidOperationException($"RevokePermission failed: {response.ErrorMessage}");
        }
    }

    /// <summary>
    /// Lists all users. Superuser-only.
    /// </summary>
    public async Task<IReadOnlyList<AdminUserInfo>> ListUsersAsync(CancellationToken cancellationToken = default) {
        var message = new ProtocolMessage { Type = CommandType.AdminListUsers };

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.Type == CommandType.Error) {
            throw new InvalidOperationException($"ListUsers failed: {response.ErrorMessage}");
        }

        if (!response.Payload.HasValue) {
            return [];
        }

        return response.Payload.Value.Deserialize<List<AdminUserInfo>>(ProtocolSerializer.Options) ?? [];
    }

    /// <summary>
    /// Returns permissions for a user. Superuser-only.
    /// </summary>
    public async Task<IReadOnlyList<AdminPermissionInfo>> GetUserPermissionsAsync(string username, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var message = new ProtocolMessage {
            Type = CommandType.AdminGetUserPermissions,
            Payload = JsonSerializer.SerializeToElement(new { username }, ProtocolSerializer.Options),
        };

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.Type == CommandType.Error) {
            throw new InvalidOperationException($"GetUserPermissions failed: {response.ErrorMessage}");
        }

        if (!response.Payload.HasValue) {
            return [];
        }

        return response.Payload.Value.Deserialize<List<AdminPermissionInfo>>(ProtocolSerializer.Options) ?? [];
    }

    /// <summary>
    /// Gracefully disconnects from the broker.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default) {
        if (_disposed) {
            return;
        }

        LogDisconnecting();
        _isConnected = false;
        await _lifetimeCts.CancelAsync().ConfigureAwait(false);

        if (_cts is not null) {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        // Send disconnect to server (best effort)
        if (_tcpClient?.Connected == true) {
            try {
                await SendMessageAsync(new ProtocolMessage {
                    Type = CommandType.Disconnect,
                }, cancellationToken).ConfigureAwait(false);
            } catch {
                // Best effort
            }
        }

        CleanupConnection();
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
        _lifetimeCts.Dispose();
        _connectLock.Dispose();
        _writeLock.Dispose();
    }

    private async Task ConnectInternalAsync(CancellationToken cancellationToken, bool isReconnect = false) {
        LogConnecting(_host, _port);

        // Reset compression so the handshake itself is always uncompressed
        _frameWriter.SetCompression(CompressionAlgorithm.None);

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(_host, _port, cancellationToken).ConfigureAwait(false);

        Stream stream = _tcpClient.GetStream();

        // TLS handshake if enabled
        if (_options.UseTls) {
            var sslStream = new SslStream(stream, leaveInnerStreamOpen: false, (_, _, _, errors) =>
                _options.SkipCertificateValidation || errors == SslPolicyErrors.None
            );

            try {
                await sslStream.AuthenticateAsClientAsync(_host).ConfigureAwait(false);
            } catch {
                await sslStream.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            stream = sslStream;
        }

        _stream = stream;
        _cts = new CancellationTokenSource();

        // Build Connect headers (auth + compression preference)
        var connectHeaders = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(_options.Username) && !string.IsNullOrEmpty(_options.Password)) {
            connectHeaders["authUsername"] = _options.Username;
            connectHeaders["authPassword"] = _options.Password;
        }
#pragma warning disable CS0618
        else if (!string.IsNullOrEmpty(_options.AuthToken)) {
            connectHeaders["authToken"] = _options.AuthToken;
        }
#pragma warning restore CS0618

        if (_options.PreferredCompressions.Count > 0) {
            connectHeaders["supported-compression"] = string.Join(
                ",",
                _options.PreferredCompressions.Select(CompressorFactory.Serialize)
            );
        }

        var connectMsg = new ProtocolMessage {
            Type = CommandType.Connect,
            Headers = connectHeaders.Count > 0 ? connectHeaders : null,
        };

        await _frameWriter.WriteFrameAsync(_stream, connectMsg, cancellationToken).ConfigureAwait(false);

        var response = await FrameReader.ReadFrameAsync(_stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE, cancellationToken)
            .ConfigureAwait(false);

        if (response is null || response.Type == CommandType.Error) {
            var reason = response?.ErrorMessage ?? "no response";
            throw new InvalidOperationException($"Connection failed: {reason}");
        }

        // Apply negotiated compression for further outgoing frames
        var negotiated = response.Headers?.GetValueOrDefault("negotiated-compression");
        var algorithm = CompressorFactory.Parse(negotiated) ?? CompressionAlgorithm.None;

        if (algorithm != CompressionAlgorithm.None) {
            _frameWriter.SetCompression(algorithm, _options.CompressionThreshold);
            LogCompressionNegotiated(algorithm);
        }

        _isConnected = true;

        // Start background tasks
        _ = ReadLoopAsync(_cts.Token);
        _ = KeepAliveLoopAsync(_cts.Token);

        // Provision declared queues (requires the read loop to be running for request/response)
        if (_options.QueueDeclarations.Count > 0) {
            try {
                await ProvisionQueuesAsync(isReconnect, cancellationToken).ConfigureAwait(false);
            } catch {
                // Provisioning failed: clean up the established connection and rethrow
                await _cts.CancelAsync().ConfigureAwait(false);
                CleanupConnection();
                throw;
            }
        }

        LogConnected(_host, _port);
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken) {
        try {
            while (!cancellationToken.IsCancellationRequested) {
                var stream = _stream;
                if (stream is null) {
                    break;
                }

                var message = await FrameReader.ReadFrameAsync(
                    stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE, cancellationToken
                ).ConfigureAwait(false);

                if (message is null) {
                    break; // Server closed connection
                }

                await HandleIncomingMessageAsync(message).ConfigureAwait(false);
            }
        } catch (OperationCanceledException) {
            // Expected on disconnect
        } catch (IOException) {
            // Connection lost
        } catch (Exception ex) {
            LogReadError(ex);
        }

        _isConnected = false;

        if (!_disposed && !_lifetimeCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            TryStartReconnect();
        }
    }

    private async Task HandleIncomingMessageAsync(ProtocolMessage message) {
        switch (message.Type) {
            case CommandType.Deliver:
                if (message.Queue is not null && _subscriptionHandlers.TryGetValue(message.Queue, out var registration)) {
                    try {
                        await registration.Handler(message, registration.CancellationToken).ConfigureAwait(false);
                    } catch (Exception ex) {
                        LogHandlerError(message.Queue, ex);
                    }
                }

                break;

            case CommandType.Pong:
                // Keep-alive response, update activity
                break;

            case CommandType.Disconnect:
                if (_logger.IsEnabled(LogLevel.Information)) {
                    var reason = message.Headers?.GetValueOrDefault("reason") ?? "unknown";
                    LogServerDisconnect(reason);
                }

                _isConnected = false;

                if (!_disposed) {
                    TryStartReconnect();
                }

                break;

            case CommandType.PublishAck:
            case CommandType.SubscribeAck:
            case CommandType.UnsubscribeAck:
            case CommandType.CreateQueue:
            case CommandType.DeleteQueue:
            case CommandType.QueueInfo:
            case CommandType.ListQueues:
            case CommandType.AdminCreateUser:
            case CommandType.AdminDeleteUser:
            case CommandType.AdminChangePassword:
            case CommandType.AdminGrantPermission:
            case CommandType.AdminRevokePermission:
            case CommandType.AdminListUsers:
            case CommandType.AdminGetUserPermissions:
            case CommandType.Error:
                // Complete pending request
                if (_pendingResponses.TryRemove(message.Id, out var tcs)) {
                    tcs.TrySetResult(message);
                }

                break;
        }
    }

    private async Task KeepAliveLoopAsync(CancellationToken cancellationToken) {
        try {
            while (!cancellationToken.IsCancellationRequested) {
                await Task.Delay(_options.KeepAliveInterval, cancellationToken).ConfigureAwait(false);

                if (IsConnected) {
                    await SendMessageAsync(new ProtocolMessage {
                        Type = CommandType.Ping,
                    }, cancellationToken).ConfigureAwait(false);
                }
            }
        } catch (OperationCanceledException) {
            // Expected
        } catch (Exception ex) {
            LogKeepAliveError(ex);
        }
    }

    private void TryStartReconnect() {
        if (Interlocked.CompareExchange(ref _reconnecting, 1, 0) == 0) {
            _ = ReconnectAsync();
        }
    }

    private async Task ReconnectAsync() {
        try {
            var policy = _options.ReconnectPolicy;
            var lifetimeToken = _lifetimeCts.Token;

            for (var attempt = 1; attempt <= policy.MaxAttempts; attempt++) {
                if (_disposed || lifetimeToken.IsCancellationRequested) {
                    return;
                }

                var delay = policy.GetDelay(attempt);
                LogReconnecting(attempt, policy.MaxAttempts, delay.TotalSeconds);

                await Task.Delay(delay, lifetimeToken).ConfigureAwait(false);

                if (_disposed || lifetimeToken.IsCancellationRequested) {
                    return;
                }

                try {
                    CleanupConnection();
                    await ConnectInternalAsync(lifetimeToken, isReconnect: true).ConfigureAwait(false);

                    // Resubscribe to all active queues
                    foreach (var queueName in _subscriptionHandlers.Keys) {
                        var response = await SendAndWaitAsync(new ProtocolMessage {
                            Type = CommandType.Subscribe,
                            Queue = queueName,
                        }, lifetimeToken).ConfigureAwait(false);

                        if (response.Type == CommandType.Error) {
                            throw new InvalidOperationException($"Resubscribe failed for queue '{queueName}': {response.ErrorMessage}");
                        }
                    }

                    LogReconnected(attempt);
                    return;
                } catch (OperationCanceledException) when (_disposed || lifetimeToken.IsCancellationRequested) {
                    return;
                } catch (Exception ex) {
                    LogReconnectFailed(attempt, ex);
                }
            }

            LogReconnectGaveUp(policy.MaxAttempts);
        } finally {
            Interlocked.Exchange(ref _reconnecting, 0);
        }
    }

    private async Task ProvisionQueuesAsync(bool isReconnect, CancellationToken cancellationToken) {
        foreach (var declaration in _options.QueueDeclarations) {
            try {
                await ProvisionSingleQueueAsync(declaration, isReconnect, cancellationToken).ConfigureAwait(false);
            } catch (QueueConflictException) {
                throw; // Always propagate conflict exceptions regardless of FailOnProvisioningError
            } catch (Exception ex) when (!declaration.FailOnProvisioningError) {
                LogProvisioningError(declaration.QueueName, ex);
            }
        }
    }

    private async Task ProvisionSingleQueueAsync(
        QueueDeclaration declaration,
        bool isReconnect,
        CancellationToken cancellationToken
    ) {
        var existing = await GetQueueInfoAsync(declaration.QueueName, cancellationToken).ConfigureAwait(false);

        if (existing is null) {
            await CreateQueueAsync(declaration.QueueName, declaration.Options, cancellationToken).ConfigureAwait(false);
            LogQueueProvisioned(declaration.QueueName);
            return;
        }

        // On reconnect, conflicts are suppressed — only ensure the queue exists
        var resolution = isReconnect ? QueueConflictResolution.Ignore : declaration.OnConflict;

        var allDiffs = QueueSettingDiffAnalyzer.Analyze(declaration.Options, existing);
        var conflicts = allDiffs.Where(d => d.Severity > ConflictSeverity.Info).ToList();

        // Log info-only diffs at Debug
        foreach (var diff in allDiffs.Where(d => d.Severity == ConflictSeverity.Info)) {
            LogSettingInfoDiff(declaration.QueueName, diff.SettingName, diff.ExistingValue, diff.DeclaredValue);
        }

        if (conflicts.Count == 0) {
            return; // Idempotent
        }

        var maxSeverity = conflicts.Max(d => d.Severity);
        var diffSummary = BuildDiffSummary(declaration.QueueName, conflicts);

        switch (resolution) {
            case QueueConflictResolution.Ignore:
                if (maxSeverity == ConflictSeverity.Hard) {
                    LogConflictError(diffSummary);
                } else {
                    LogConflictWarning(diffSummary);
                }

                break;

            case QueueConflictResolution.Fail:
                throw new QueueConflictException(declaration.QueueName, conflicts);

            case QueueConflictResolution.Override:
                if (maxSeverity == ConflictSeverity.Hard) {
                    LogOverrideHard(declaration.QueueName);
                } else {
                    LogOverrideSoft(declaration.QueueName);
                }

                await DeleteQueueAsync(declaration.QueueName, cancellationToken).ConfigureAwait(false);
                await CreateQueueAsync(declaration.QueueName, declaration.Options, cancellationToken).ConfigureAwait(false);
                LogQueueProvisioned(declaration.QueueName);
                break;
        }
    }

    private static string BuildDiffSummary(string queueName, IReadOnlyList<QueueSettingDiff> conflicts) {
        var severities = conflicts.Select(c => c.Severity.ToString()).Distinct().OrderByDescending(s => s);
        var sb = new StringBuilder();
        sb.Append(System.Globalization.CultureInfo.InvariantCulture,
            $"Queue '{queueName}' has conflicting settings [{string.Join(", ", severities)}]:");

        foreach (var diff in conflicts) {
            sb.AppendLine();
            var existing = diff.ExistingValue is null ? "null" : diff.ExistingValue.ToString()!;
            var declared = diff.DeclaredValue is null ? "null" : diff.DeclaredValue.ToString()!;
            sb.Append(System.Globalization.CultureInfo.InvariantCulture,
                $"  [{diff.Severity}] {diff.SettingName,-20} {existing,-15} →  {declared}  (declared)");
        }

        return sb.ToString();
    }

    private async Task<ProtocolMessage> SendAndWaitAsync(ProtocolMessage message, CancellationToken cancellationToken) {
        var tcs = new TaskCompletionSource<ProtocolMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingResponses.TryAdd(message.Id, tcs);

        try {
            await SendMessageAsync(message, cancellationToken).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.CommandTimeout);

            try {
                return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            } catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested) {
                throw new TimeoutException(
                    $"Timed out waiting for broker response to '{message.Type}' after {_options.CommandTimeout.TotalSeconds:0.###}s.",
                    ex
                );
            }
        } catch {
            _pendingResponses.TryRemove(message.Id, out _);
            throw;
        }
    }

    private async Task SendMessageAsync(ProtocolMessage message, CancellationToken cancellationToken) {
        var stream = _stream;
        if (stream is null) {
            throw new InvalidOperationException("Not connected to broker.");
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            await _frameWriter.WriteFrameAsync(stream, message, cancellationToken).ConfigureAwait(false);
        } finally {
            _writeLock.Release();
        }
    }

    private void CleanupConnection() {
        try {
            _stream?.Close();
        } catch {
            /* ignore */
        }

        try {
            _tcpClient?.Close();
        } catch {
            /* ignore */
        }

        try {
            _cts?.Dispose();
        } catch {
            /* ignore */
        }

        _stream = null;
        _tcpClient = null;
        _cts = null;

        // Cancel pending responses
        foreach (var (id, tcs) in _pendingResponses) {
            tcs.TrySetCanceled();
            _pendingResponses.TryRemove(id, out _);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to VibeMQ broker at {host}:{port}...")]
    private partial void LogConnecting(string host, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to VibeMQ broker at {host}:{port}.")]
    private partial void LogConnected(string host, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "Disconnecting from VibeMQ broker...")]
    private partial void LogDisconnecting();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Subscribed to queue {queueName}.")]
    private partial void LogSubscribed(string queueName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Unsubscribed from queue {queueName}.")]
    private partial void LogUnsubscribed(string queueName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created queue {queueName}.")]
    private partial void LogQueueCreated(string queueName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted queue {queueName}.")]
    private partial void LogQueueDeleted(string queueName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error in read loop.")]
    private partial void LogReadError(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error in subscription handler for queue {queueName}.")]
    private partial void LogHandlerError(string queueName, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Keep-alive error.")]
    private partial void LogKeepAliveError(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Server requested disconnect. Reason: {reason}.")]
    private partial void LogServerDisconnect(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reconnecting (attempt {attempt}/{maxAttempts}, delay: {delaySeconds}s)...")]
    private partial void LogReconnecting(int attempt, int maxAttempts, double delaySeconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reconnected successfully after {attempt} attempt(s).")]
    private partial void LogReconnected(int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reconnect attempt {attempt} failed.")]
    private partial void LogReconnectFailed(int attempt, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Reconnect gave up after {maxAttempts} attempts.")]
    private partial void LogReconnectGaveUp(int maxAttempts);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Compression negotiated with broker: {algorithm}.")]
    private partial void LogCompressionNegotiated(CompressionAlgorithm algorithm);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Queue '{queueName}' provisioned successfully.")]
    private partial void LogQueueProvisioned(string queueName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Provisioning queue '{queueName}' failed; continuing because FailOnProvisioningError is false.")]
    private partial void LogProvisioningError(string queueName, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Queue '{queueName}' settings mismatch (Soft conflict), overriding as per OnConflict=Override.")]
    private partial void LogOverrideSoft(string queueName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Queue '{queueName}' settings mismatch (Hard conflict), overriding as per OnConflict=Override.")]
    private partial void LogOverrideHard(string queueName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Queue '{queueName}' setting '{settingName}': {existingValue} → {declaredValue} (Info, not a conflict).")]
    private partial void LogSettingInfoDiff(string queueName, string settingName, object? existingValue, object? declaredValue);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{diffSummary}")]
    private partial void LogConflictWarning(string diffSummary);

    [LoggerMessage(Level = LogLevel.Error, Message = "{diffSummary}")]
    private partial void LogConflictError(string diffSummary);

    private sealed class SubscriptionRegistration(
        string queueName,
        Func<CancellationToken, ValueTask> disposeResources
    ) : IAsyncDisposable {
        private Func<ProtocolMessage, CancellationToken, Task> _handler = static (_, _) => Task.CompletedTask;
        private readonly CancellationTokenSource _subscriptionCts = new();
        private int _disposed;

        public string QueueName { get; } = queueName;
        public Guid SubscriptionId { get; } = Guid.NewGuid();
        public CancellationToken CancellationToken => _subscriptionCts.Token;
        public Func<ProtocolMessage, CancellationToken, Task> Handler => _handler;

        public void SetHandler(Func<ProtocolMessage, CancellationToken, Task> handler) {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) {
                return;
            }

            await _subscriptionCts.CancelAsync().ConfigureAwait(false);
            _subscriptionCts.Dispose();
            await disposeResources(CancellationToken.None).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Represents an active subscription. Unsubscribes when disposed.
/// </summary>
file sealed class Subscription(VibeMQClient client, string queueName, Guid subscriptionId) : IAsyncDisposable {
    public async ValueTask DisposeAsync() {
        await client.UnsubscribeAsync(queueName, subscriptionId).ConfigureAwait(false);
    }
}
