using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using VibeMQ.Configuration;
using VibeMQ.Metrics;
using VibeMQ.Protocol;
using VibeMQ.Server.Connections;
using VibeMQ.Server.Delivery;
using VibeMQ.Server.Handlers;
using VibeMQ.Server.Queues;
using VibeMQ.Server.Security;

namespace VibeMQ.Server;

/// <summary>
/// Main entry point for the VibeMQ message broker.
/// Manages the server lifecycle: start, accept connections, read loop, graceful shutdown.
/// </summary>
public sealed partial class BrokerServer : IAsyncDisposable {
    private static readonly TimeSpan _shutdownGracePeriod = TimeSpan.FromSeconds(30);

    private readonly BrokerOptions _options;
    private readonly ConnectionManager _connectionManager;
    private readonly CommandDispatcher _commandDispatcher;
    private readonly QueueManager _queueManager;
    private readonly AckTracker _ackTracker;
    private readonly RateLimiter _rateLimiter;
    private readonly IBrokerMetrics _metrics;
    private readonly ILogger<BrokerServer> _logger;
    private readonly CancellationTokenSource _shutdownCts = new();
    private TcpListener? _listener;

    public BrokerServer(
        BrokerOptions options,
        ConnectionManager connectionManager,
        CommandDispatcher commandDispatcher,
        QueueManager queueManager,
        AckTracker ackTracker,
        RateLimiter rateLimiter,
        IBrokerMetrics metrics,
        ILogger<BrokerServer> logger
    ) {
        _options = options;
        _connectionManager = connectionManager;
        _commandDispatcher = commandDispatcher;
        _queueManager = queueManager;
        _ackTracker = ackTracker;
        _rateLimiter = rateLimiter;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Provides read-only access to broker metrics.
    /// </summary>
    public IBrokerMetrics Metrics => _metrics;

    /// <summary>
    /// Number of currently active client connections.
    /// </summary>
    public int ActiveConnections => _connectionManager.ActiveCount;

    /// <summary>
    /// Number of unacknowledged in-flight messages.
    /// </summary>
    public int InFlightMessages => _ackTracker.PendingCount;

    /// <summary>
    /// Starts the broker and blocks until shutdown is requested.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default) {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
        var token = linkedCts.Token;

        _ackTracker.Start();

        _listener = new TcpListener(IPAddress.Any, _options.Port);
        _listener.Start();

        // Start background gauge metrics updater
        _ = UpdateGaugeMetricsLoopAsync(token);

        LogServerStarted(_options.Port, _options.MaxConnections, _options.Tls.Enabled);

        try {
            while (!token.IsCancellationRequested) {
                var tcpClient = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                _ = HandleClientAsync(tcpClient, token);
            }
        } catch (OperationCanceledException) {
            // Expected on shutdown
        } catch (SocketException) {
            // Expected when listener is stopped during shutdown
        } catch (ObjectDisposedException) {
            // Expected when listener is disposed during shutdown
        } finally {
            _listener.Stop();
            LogServerStopped();
        }
    }

    /// <summary>
    /// Initiates graceful shutdown:
    /// 1. Stop accepting new connections
    /// 2. Notify clients about shutdown
    /// 3. Wait for in-flight messages with timeout
    /// 4. Close all connections
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default) {
        LogServerShuttingDown();

        // Cancel first, then stop listener to avoid SocketException race
        await _shutdownCts.CancelAsync().ConfigureAwait(false);
        _listener?.Stop();

        var connections = _connectionManager.GetAll();

        foreach (var connection in connections) {
            try {
                await connection.SendMessageAsync(new ProtocolMessage {
                    Type = CommandType.Disconnect,
                    Headers = new Dictionary<string, string> {
                        ["reason"] = "server_shutdown",
                    },
                }, cancellationToken).ConfigureAwait(false);
            } catch {
                // Best effort notification
            }
        }

        await WaitForInFlightMessagesAsync(_shutdownGracePeriod, cancellationToken).ConfigureAwait(false);
        await _ackTracker.DisposeAsync().ConfigureAwait(false);
        await _connectionManager.DisposeAsync().ConfigureAwait(false);
    }

    private async Task WaitForInFlightMessagesAsync(TimeSpan gracePeriod, CancellationToken cancellationToken) {
        if (_ackTracker.PendingCount == 0) {
            return;
        }

        LogWaitingForInFlight(_ackTracker.PendingCount, gracePeriod.TotalSeconds);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(gracePeriod);

        try {
            while (_ackTracker.PendingCount > 0 && !timeoutCts.Token.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromMilliseconds(250), timeoutCts.Token).ConfigureAwait(false);
            }
        } catch (OperationCanceledException) {
            // Timeout reached
        }

        if (_ackTracker.PendingCount > 0) {
            LogInFlightTimeout(_ackTracker.PendingCount);
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken) {
        var remoteEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
        var ipAddress = remoteEndPoint?.Address.ToString() ?? "unknown";

        // Rate limit: connections per IP
        if (!_rateLimiter.IsConnectionAllowed(ipAddress)) {
            _metrics.RecordConnectionRejected();
            tcpClient.Close();
            return;
        }

        var connectionId = Guid.NewGuid().ToString("N");
        var connection = new ClientConnection(connectionId, tcpClient, _options.MaxMessageSize, _logger);

        // TLS handshake if enabled
        if (_options.Tls.Enabled) {
            try {
                var sslStream = await TlsHelper.AuthenticateAsServerAsync(
                    tcpClient.GetStream(), _options.Tls, cancellationToken
                ).ConfigureAwait(false);

                connection.UpgradeToTls(sslStream);
                LogTlsHandshakeCompleted(connectionId);
            } catch (Exception ex) {
                LogTlsHandshakeFailed(connectionId, ex);
                await connection.DisposeAsync().ConfigureAwait(false);
                return;
            }
        }

        if (!_connectionManager.TryAdd(connection)) {
            try {
                await connection.SendErrorAsync("CONNECTION_LIMIT", "Maximum connections reached.", cancellationToken)
                    .ConfigureAwait(false);
            } catch {
                // Ignore send errors on rejected connections
            }

            await connection.DisposeAsync().ConfigureAwait(false);
            return;
        }

        try {
            await ReadLoopAsync(connection, cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) {
            // Expected on shutdown
        } catch (IOException) {
            LogClientDisconnectedAbruptly(connectionId);
        } catch (Exception ex) {
            _metrics.RecordError();
            LogClientError(connectionId, ex);
        } finally {
            _rateLimiter.RemoveClient(connectionId);
            await _connectionManager.RemoveAsync(connectionId).ConfigureAwait(false);
        }
    }

    private async Task ReadLoopAsync(ClientConnection connection, CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested && connection.IsConnected) {
            var message = await connection.ReadMessageAsync(cancellationToken).ConfigureAwait(false);

            if (message is null) {
                break;
            }

            // Rate limit: messages per client
            if (!_rateLimiter.IsMessageAllowed(connection.Id)) {
                await connection.SendErrorAsync("RATE_LIMITED", "Message rate limit exceeded.", cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            // Validate message
            var validationError = MessageValidator.Validate(message);

            if (validationError is not null) {
                await connection.SendErrorAsync("INVALID_MESSAGE", validationError, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (!connection.IsAuthenticated && message.Type != CommandType.Connect && message.Type != CommandType.Ping) {
                await connection.SendErrorAsync("NOT_AUTHENTICATED", "Please send a Connect command first.", cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            await _commandDispatcher.DispatchAsync(connection, message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task UpdateGaugeMetricsLoopAsync(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            try {
                _metrics.UpdateGauges(
                    _connectionManager.ActiveCount,
                    _queueManager.QueueCount,
                    _ackTracker.PendingCount
                );

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                break;
            }
        }
    }

    public async ValueTask DisposeAsync() {
        try {
            await StopAsync().ConfigureAwait(false);
        } catch {
            // Best effort cleanup
        }

        _shutdownCts.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "VibeMQ server started on port {port}. Max connections: {maxConnections}. TLS: {tlsEnabled}.")]
    private partial void LogServerStarted(int port, int maxConnections, bool tlsEnabled);

    [LoggerMessage(Level = LogLevel.Information, Message = "VibeMQ server shutting down...")]
    private partial void LogServerShuttingDown();

    [LoggerMessage(Level = LogLevel.Information, Message = "VibeMQ server stopped.")]
    private partial void LogServerStopped();

    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for {count} in-flight messages (grace period: {seconds}s)...")]
    private partial void LogWaitingForInFlight(int count, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Shutdown grace period expired. {count} messages still unacknowledged.")]
    private partial void LogInFlightTimeout(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "TLS handshake completed for client {clientId}.")]
    private partial void LogTlsHandshakeCompleted(string clientId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "TLS handshake failed for client {clientId}.")]
    private partial void LogTlsHandshakeFailed(string clientId, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {clientId} disconnected abruptly.")]
    private partial void LogClientDisconnectedAbruptly(string clientId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error handling client {clientId}.")]
    private partial void LogClientError(string clientId, Exception exception);
}
