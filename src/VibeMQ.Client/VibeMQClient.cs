using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VibeMQ.Protocol;
using VibeMQ.Protocol.Framing;

namespace VibeMQ.Client;

/// <summary>
/// Client for connecting to a VibeMQ message broker.
/// Handles connection, publish/subscribe, keep-alive, reconnection, and graceful disconnect.
/// </summary>
public sealed partial class VibeMQClient : IAsyncDisposable {
    private readonly ClientOptions _options;
    private readonly ILogger<VibeMQClient> _logger;
    private readonly ConcurrentDictionary<string, Func<ProtocolMessage, Task>> _subscriptionHandlers = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtocolMessage>> _pendingResponses = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private string _host = string.Empty;
    private int _port;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _readLoopTask;
    private Task? _keepAliveTask;
    private bool _disposed;
    private bool _isConnected;

    private VibeMQClient(ClientOptions options, ILogger<VibeMQClient>? logger) {
        _options = options;
        _logger = logger ?? NullLogger<VibeMQClient>.Instance;
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
        var client = new VibeMQClient(options ?? new ClientOptions(), logger);
        client._host = host;
        client._port = port;

        await client.ConnectInternalAsync(cancellationToken).ConfigureAwait(false);
        return client;
    }

    /// <summary>
    /// Publishes a message to the specified queue.
    /// </summary>
    public async Task PublishAsync<T>(string queueName, T payload, CancellationToken cancellationToken = default) {
        var jsonPayload = JsonSerializer.SerializeToElement(payload, ProtocolSerializer.Options);

        var message = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = queueName,
            Payload = jsonPayload,
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
        // Register the handler
        _subscriptionHandlers[queueName] = async protocolMessage => {
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
            }, cancellationToken).ConfigureAwait(false);
        };

        // Send Subscribe command
        var message = new ProtocolMessage {
            Type = CommandType.Subscribe,
            Queue = queueName,
        };

        var response = await SendAndWaitAsync(message, cancellationToken).ConfigureAwait(false);

        if (response.Type == CommandType.Error) {
            _subscriptionHandlers.TryRemove(queueName, out _);
            throw new InvalidOperationException($"Subscribe failed: {response.ErrorMessage}");
        }

        LogSubscribed(queueName);
        return new Subscription(this, queueName);
    }

    /// <summary>
    /// Unsubscribes from a queue.
    /// </summary>
    public async Task UnsubscribeAsync(string queueName, CancellationToken cancellationToken = default) {
        _subscriptionHandlers.TryRemove(queueName, out _);

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
        _stream = _tcpClient.GetStream();

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
                if (message.Queue is not null && _subscriptionHandlers.TryGetValue(message.Queue, out var handler)) {
                    try {
                        await handler(message).ConfigureAwait(false);
                    } catch (Exception ex) {
                        LogHandlerError(message.Queue, ex);
                    }
                }
                break;

            case CommandType.Pong:
                // Keep-alive response, update activity
                break;

            case CommandType.Disconnect:
                LogServerDisconnect(message.Headers?.GetValueOrDefault("reason") ?? "unknown");
                _isConnected = false;

                if (!_disposed) {
                    _ = ReconnectAsync();
                }
                break;

            case CommandType.PublishAck:
            case CommandType.SubscribeAck:
            case CommandType.UnsubscribeAck:
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
}

/// <summary>
/// Represents an active subscription. Unsubscribes when disposed.
/// </summary>
file sealed class Subscription : IAsyncDisposable {
    private readonly VibeMQClient _client;
    private readonly string _queueName;

    public Subscription(VibeMQClient client, string queueName) {
        _client = client;
        _queueName = queueName;
    }

    public async ValueTask DisposeAsync() {
        await _client.UnsubscribeAsync(_queueName).ConfigureAwait(false);
    }
}
