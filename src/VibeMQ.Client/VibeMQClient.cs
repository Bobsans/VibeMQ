using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VibeMQ.Configuration;
using VibeMQ.Interfaces;
using VibeMQ.Models;
using VibeMQ.Protocol;
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

    private string _host = string.Empty;
    private int _port;
    private TcpClient? _tcpClient;
    private Stream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _readLoopTask;
    private Task? _keepAliveTask;
    private bool _disposed;
    private bool _isConnected;

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
        var client = new VibeMQClient(options ?? new ClientOptions(), logger, serviceProvider);
        client._host = host;
        client._port = port;

        await client.ConnectInternalAsync(cancellationToken).ConfigureAwait(false);
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

        if (expectedSubscriptionId is Guid subscriptionId &&
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

    /// <summary>
    /// Gracefully disconnects from the broker.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default) {
        if (_disposed) {
            return;
        }

        LogDisconnecting();
        _isConnected = false;

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
        _connectLock.Dispose();
    }

    private async Task ConnectInternalAsync(CancellationToken cancellationToken) {
        LogConnecting(_host, _port);

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(_host, _port, cancellationToken).ConfigureAwait(false);

        Stream stream = _tcpClient.GetStream();

        // TLS handshake if enabled
        if (_options.UseTls) {
            var sslStream = new SslStream(stream, leaveInnerStreamOpen: false, (_, _, _, errors) =>
                _options.SkipCertificateValidation || errors == SslPolicyErrors.None
            );

            await sslStream.AuthenticateAsClientAsync(_host).ConfigureAwait(false);
            stream = sslStream;
        }

        _stream = stream;
        _cts = new CancellationTokenSource();

        // Authenticate if required
        if (!string.IsNullOrEmpty(_options.AuthToken)) {
            var connectMsg = new ProtocolMessage {
                Type = CommandType.Connect,
                Headers = new Dictionary<string, string> {
                    ["authToken"] = _options.AuthToken,
                },
            };

            await FrameWriter.WriteFrameAsync(_stream, connectMsg, cancellationToken).ConfigureAwait(false);
            var response = await FrameReader.ReadFrameAsync(_stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE, cancellationToken)
                .ConfigureAwait(false);

            if (response is null || response.Type == CommandType.Error) {
                throw new InvalidOperationException($"Authentication failed: {response?.ErrorMessage ?? "no response"}");
            }
        } else {
            // Send Connect without auth
            var connectMsg = new ProtocolMessage { Type = CommandType.Connect };
            await FrameWriter.WriteFrameAsync(_stream, connectMsg, cancellationToken).ConfigureAwait(false);
            var response = await FrameReader.ReadFrameAsync(_stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE, cancellationToken)
                .ConfigureAwait(false);

            if (response is null || response.Type == CommandType.Error) {
                throw new InvalidOperationException($"Connection failed: {response?.ErrorMessage ?? "no response"}");
            }
        }

        _isConnected = true;

        // Start background tasks
        _readLoopTask = ReadLoopAsync(_cts.Token);
        _keepAliveTask = KeepAliveLoopAsync(_cts.Token);

        LogConnected(_host, _port);
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken) {
        try {
            while (!cancellationToken.IsCancellationRequested && _stream is not null) {
                var message = await FrameReader.ReadFrameAsync(
                    _stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE, cancellationToken
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

        if (!_disposed && !cancellationToken.IsCancellationRequested) {
            _ = ReconnectAsync();
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
                    LogServerDisconnect(message.Headers?.GetValueOrDefault("reason") ?? "unknown");
                }
                _isConnected = false;

                if (!_disposed) {
                    _ = ReconnectAsync();
                }
                break;

            case CommandType.PublishAck:
            case CommandType.SubscribeAck:
            case CommandType.UnsubscribeAck:
            case CommandType.CreateQueue:
            case CommandType.DeleteQueue:
            case CommandType.QueueInfo:
            case CommandType.ListQueues:
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

    private async Task ReconnectAsync() {
        var policy = _options.ReconnectPolicy;

        for (var attempt = 1; attempt <= policy.MaxAttempts; attempt++) {
            if (_disposed) {
                return;
            }

            var delay = policy.GetDelay(attempt);
            LogReconnecting(attempt, policy.MaxAttempts, delay.TotalSeconds);

            await Task.Delay(delay).ConfigureAwait(false);

            try {
                CleanupConnection();
                await ConnectInternalAsync(CancellationToken.None).ConfigureAwait(false);

                // Resubscribe to all active queues
                foreach (var queueName in _subscriptionHandlers.Keys) {
                    await SendMessageAsync(new ProtocolMessage {
                        Type = CommandType.Subscribe,
                        Queue = queueName,
                    }, CancellationToken.None).ConfigureAwait(false);
                }

                LogReconnected(attempt);
                return;
            } catch (Exception ex) {
                LogReconnectFailed(attempt, ex);
            }
        }

        LogReconnectGaveUp(policy.MaxAttempts);
    }

    private async Task<ProtocolMessage> SendAndWaitAsync(ProtocolMessage message, CancellationToken cancellationToken) {
        var tcs = new TaskCompletionSource<ProtocolMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingResponses.TryAdd(message.Id, tcs);

        try {
            await SendMessageAsync(message, cancellationToken).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.CommandTimeout);

            var registration = timeoutCts.Token.Register(() => tcs.TrySetCanceled());

            try {
                return await tcs.Task.ConfigureAwait(false);
            } finally {
                await registration.DisposeAsync().ConfigureAwait(false);
            }
        } catch {
            _pendingResponses.TryRemove(message.Id, out _);
            throw;
        }
    }

    private async Task SendMessageAsync(ProtocolMessage message, CancellationToken cancellationToken) {
        if (_stream is null) {
            throw new InvalidOperationException("Not connected to broker.");
        }

        await FrameWriter.WriteFrameAsync(_stream, message, cancellationToken).ConfigureAwait(false);
    }

    private void CleanupConnection() {
        try { _stream?.Close(); } catch { /* ignore */ }
        try { _tcpClient?.Close(); } catch { /* ignore */ }

        _stream = null;
        _tcpClient = null;

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

    private sealed class SubscriptionRegistration : IAsyncDisposable {
        private Func<ProtocolMessage, CancellationToken, Task> _handler;
        private readonly Func<CancellationToken, ValueTask> _disposeResources;
        private readonly CancellationTokenSource _subscriptionCts = new();
        private int _disposed;

        public SubscriptionRegistration(
            string queueName,
            Func<CancellationToken, ValueTask> disposeResources
        ) {
            QueueName = queueName;
            _handler = static (_, _) => Task.CompletedTask;
            _disposeResources = disposeResources;
        }

        public string QueueName { get; }
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
            await _disposeResources(CancellationToken.None).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Represents an active subscription. Unsubscribes when disposed.
/// </summary>
file sealed class Subscription : IAsyncDisposable {
    private readonly VibeMQClient _client;
    private readonly string _queueName;
    private readonly Guid _subscriptionId;

    public Subscription(VibeMQClient client, string queueName, Guid subscriptionId) {
        _client = client;
        _queueName = queueName;
        _subscriptionId = subscriptionId;
    }

    public async ValueTask DisposeAsync() {
        await _client.UnsubscribeAsync(_queueName, _subscriptionId).ConfigureAwait(false);
    }
}
