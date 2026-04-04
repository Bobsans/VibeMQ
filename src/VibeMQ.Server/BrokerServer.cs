using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using Microsoft.Extensions.Logging;
using VibeMQ.Configuration;
using VibeMQ.Interfaces;
using VibeMQ.Metrics;
using VibeMQ.Protocol;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;
using VibeMQ.Server.Delivery;
using VibeMQ.Server.Handlers;
using VibeMQ.Server.Queues;
using VibeMQ.Models;
using VibeMQ.Server.Security;

namespace VibeMQ.Server;

/// <summary>
/// Main entry point for the VibeMQ message broker.
/// Manages the server lifecycle: start, accept connections, read loop, graceful shutdown.
/// </summary>
public sealed partial class BrokerServer(
    BrokerOptions options,
    ConnectionManager connectionManager,
    CommandDispatcher commandDispatcher,
    QueueManager queueManager,
    AckTracker ackTracker,
    RateLimiter rateLimiter,
    IStorageProvider storageProvider,
    IBrokerMetrics metrics,
    AuthBootstrapper? authBootstrapper,
    ILogger<BrokerServer> logger
) : IAsyncDisposable {
    private static readonly TimeSpan _shutdownGracePeriod = TimeSpan.FromSeconds(30);
    // LoggerMessage source generation for net8 requires an ILogger field/property on the type.
    private readonly ILogger<BrokerServer> _logger = logger;

    private readonly CancellationTokenSource _shutdownCts = new();
    private TcpListener? _listener;

    /// <summary>
    /// Provides read-only access to broker metrics.
    /// </summary>
    public IBrokerMetrics Metrics => metrics;

    /// <summary>
    /// Number of currently active client connections.
    /// </summary>
    public int ActiveConnections => connectionManager.ActiveCount;

    /// <summary>
    /// Number of unacknowledged in-flight messages.
    /// </summary>
    public int InFlightMessages => ackTracker.PendingCount;

    /// <summary>
    /// Number of active queues. Used by dashboard and health.
    /// </summary>
    public int QueueCount => queueManager.QueueCount;

    /// <summary>
    /// Lists all queue names. Used by the Web UI dashboard (read-only).
    /// </summary>
    public Task<IReadOnlyList<string>> ListQueuesAsync(CancellationToken cancellationToken = default) =>
        queueManager.ListQueuesAsync(cancellationToken);

    /// <summary>
    /// Returns metadata for a single queue. Used by the Web UI dashboard (read-only).
    /// </summary>
    public Task<QueueInfo?> GetQueueInfoAsync(string name, CancellationToken cancellationToken = default) =>
        queueManager.GetQueueInfoAsync(name, cancellationToken);

    /// <summary>
    /// Returns a slice of pending messages for the dashboard (peek). Used by Web UI.
    /// </summary>
    public Task<IReadOnlyList<BrokerMessage>> GetPendingMessagesForDashboardAsync(string name, int limit, int offset, CancellationToken cancellationToken = default) =>
        queueManager.GetPendingMessagesForDashboardAsync(name, limit, offset, cancellationToken);

    /// <summary>
    /// Returns a single message by id from a queue for dashboard view.
    /// </summary>
    public Task<BrokerMessage?> GetMessageForDashboardAsync(string name, string messageId, CancellationToken cancellationToken = default) =>
        queueManager.GetMessageForDashboardAsync(name, messageId, cancellationToken);

    /// <summary>
    /// Removes a message from the queue and storage (dashboard admin).
    /// </summary>
    public Task<bool> RemoveMessageFromQueueAsync(string name, string messageId, CancellationToken cancellationToken = default) =>
        queueManager.RemoveMessageFromQueueAsync(name, messageId, cancellationToken);

    /// <summary>
    /// Purges all pending messages from a queue (dashboard admin).
    /// </summary>
    public Task<bool> PurgeQueueAsync(string name, CancellationToken cancellationToken = default) =>
        queueManager.PurgeQueueAsync(name, cancellationToken);

    /// <summary>
    /// Deletes a queue and all its messages (dashboard admin).
    /// </summary>
    public Task DeleteQueueAsync(string name, CancellationToken cancellationToken = default) =>
        queueManager.DeleteQueueAsync(name, cancellationToken);

    /// <summary>
    /// Starts the broker and blocks until shutdown is requested.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default) {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
        var token = linkedCts.Token;

        // Initialize authorization (schema + superuser seed)
        if (authBootstrapper is not null) {
            await authBootstrapper.InitializeAsync(token).ConfigureAwait(false);
        }

        // Initialize storage and recover persisted state
        await queueManager.InitializeAsync(token).ConfigureAwait(false);

        ackTracker.Start();

        if (options.Tls.Enabled && options.Tls.SslProtocols.HasFlag(SslProtocols.Tls12)) {
            LogTls12EnabledWarning();
        }

        _listener = new TcpListener(IPAddress.Any, options.Port);
        _listener.Start();

        // Start background gauge metrics updater
        _ = UpdateGaugeMetricsLoopAsync(token);

        LogServerStarted(options.Port, options.MaxConnections, options.Tls.Enabled);

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

        var connections = connectionManager.GetAll();

        foreach (var connection in connections) {
            try {
                await connection.SendMessageAsync(new ProtocolMessage {
                    Type = CommandType.Disconnect,
                    Headers = new Dictionary<string, string> {
                        ["reason"] = "server_shutdown"
                    }
                }, cancellationToken).ConfigureAwait(false);
            } catch {
                // Best effort notification
            }
        }

        await WaitForInFlightMessagesAsync(_shutdownGracePeriod, cancellationToken).ConfigureAwait(false);
        await ackTracker.DisposeAsync().ConfigureAwait(false);
        await connectionManager.DisposeAsync().ConfigureAwait(false);
        await storageProvider.DisposeAsync().ConfigureAwait(false);
    }

    private async Task WaitForInFlightMessagesAsync(TimeSpan gracePeriod, CancellationToken cancellationToken) {
        if (ackTracker.PendingCount == 0) {
            return;
        }

        LogWaitingForInFlight(ackTracker.PendingCount, gracePeriod.TotalSeconds);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(gracePeriod);

        try {
            while (ackTracker.PendingCount > 0 && !timeoutCts.Token.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromMilliseconds(250), timeoutCts.Token).ConfigureAwait(false);
            }
        } catch (OperationCanceledException) {
            // Timeout reached
        }

        if (ackTracker.PendingCount > 0) {
            LogInFlightTimeout(ackTracker.PendingCount);
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken) {
        var remoteEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
        var ipAddress = remoteEndPoint?.Address.ToString() ?? "unknown";

        // Rate limit: connections per IP
        if (!rateLimiter.IsConnectionAllowed(ipAddress)) {
            metrics.RecordConnectionRejected();
            tcpClient.Close();
            return;
        }

        var connectionId = Guid.NewGuid().ToString("N");
        var connection = new ClientConnection(connectionId, tcpClient, options.MaxMessageSize, _logger);

        // TLS handshake if enabled
        if (options.Tls.Enabled) {
            try {
                var sslStream = await TlsHelper.AuthenticateAsServerAsync(
                    tcpClient.GetStream(), options.Tls, cancellationToken
                ).ConfigureAwait(false);

                connection.UpgradeToTls(sslStream);
                LogTlsHandshakeCompleted(connectionId);
            } catch (Exception ex) {
                LogTlsHandshakeFailed(connectionId, ex);
                await connection.DisposeAsync().ConfigureAwait(false);
                return;
            }
        }

        if (!connectionManager.TryAdd(connection)) {
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
            metrics.RecordError();
            LogClientError(connectionId, ex);
        } finally {
            // Requeue unacknowledged messages for this client back into their queues
            var orphaned = ackTracker.RemoveAllForClient(connectionId);
            queueManager.RequeueMessages(orphaned);

            rateLimiter.RemoveClient(connectionId);
            await connectionManager.RemoveAsync(connectionId).ConfigureAwait(false);
        }
    }

    private async Task ReadLoopAsync(ClientConnection connection, CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested && connection.IsConnected) {
            var message = await connection.ReadMessageAsync(cancellationToken).ConfigureAwait(false);

            if (message is null) {
                break;
            }

            // Rate limit: messages per client
            if (!rateLimiter.IsMessageAllowed(connection.Id)) {
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

            // Client-initiated graceful disconnect: exit read loop so connection is cleaned up in finally
            if (message.Type == CommandType.Disconnect) {
                LogClientDisconnected(connection.Id);
                break;
            }

            await commandDispatcher.DispatchAsync(connection, message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task UpdateGaugeMetricsLoopAsync(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            try {
                metrics.UpdateGauges(
                    connectionManager.ActiveCount,
                    queueManager.QueueCount,
                    ackTracker.PendingCount
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "TLS 1.2 is enabled. Prefer TLS 1.3 unless legacy compatibility is required.")]
    private partial void LogTls12EnabledWarning();

    [LoggerMessage(Level = LogLevel.Information, Message = "Client {clientId} disconnected.")]
    private partial void LogClientDisconnected(string clientId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {clientId} disconnected abruptly.")]
    private partial void LogClientDisconnectedAbruptly(string clientId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error handling client {clientId}.")]
    private partial void LogClientError(string clientId, Exception exception);
}
