using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using VibeMQ.Core.Metrics;

namespace VibeMQ.Server.Connections;

/// <summary>
/// Manages all active client connections. Provides add/remove/lookup
/// and enforces the maximum connection limit.
/// </summary>
public sealed partial class ConnectionManager : IAsyncDisposable {
    private readonly ConcurrentDictionary<string, ClientConnection> _connections = new();
    private readonly int _maxConnections;
    private readonly IBrokerMetrics _metrics;
    private readonly ILogger<ConnectionManager> _logger;

    public ConnectionManager(int maxConnections, IBrokerMetrics metrics, ILogger<ConnectionManager> logger) {
        _maxConnections = maxConnections;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Number of currently active connections.
    /// </summary>
    public int ActiveCount => _connections.Count;

    /// <summary>
    /// Tries to register a new connection. Returns false if the connection limit is reached.
    /// </summary>
    public bool TryAdd(ClientConnection connection) {
        if (_connections.Count >= _maxConnections) {
            LogConnectionLimitReached(_maxConnections);
            _metrics.RecordConnectionRejected();
            return false;
        }

        if (!_connections.TryAdd(connection.Id, connection)) {
            return false;
        }

        _metrics.RecordConnectionAccepted();
        if (_logger.IsEnabled(LogLevel.Information)) {
            LogClientConnected(connection.Id, connection.RemoteEndPoint?.ToString() ?? "unknown", ActiveCount);
        }
        return true;
    }

    /// <summary>
    /// Removes a connection by its ID and disposes it.
    /// </summary>
    public async Task RemoveAsync(string connectionId) {
        if (_connections.TryRemove(connectionId, out var connection)) {
            LogClientDisconnected(connectionId, ActiveCount);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets a connection by ID.
    /// </summary>
    public ClientConnection? Get(string connectionId) {
        _connections.TryGetValue(connectionId, out var connection);
        return connection;
    }

    /// <summary>
    /// Returns all active connections.
    /// </summary>
    public IReadOnlyCollection<ClientConnection> GetAll() {
        return _connections.Values.ToArray();
    }

    /// <summary>
    /// Returns all connections subscribed to a specific queue.
    /// </summary>
    public IReadOnlyList<ClientConnection> GetSubscribers(string queueName) {
        return _connections.Values
            .Where(c => c.Subscriptions.Contains(queueName))
            .ToArray();
    }

    /// <summary>
    /// Disposes all connections (used during graceful shutdown).
    /// </summary>
    public async ValueTask DisposeAsync() {
        var connections = _connections.Values.ToArray();
        _connections.Clear();

        foreach (var connection in connections) {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection limit reached ({maxConnections}). Rejecting new connection.")]
    private partial void LogConnectionLimitReached(int maxConnections);

    [LoggerMessage(Level = LogLevel.Information, Message = "Client {clientId} connected from {remoteEndPoint}. Active: {activeCount}.")]
    private partial void LogClientConnected(string clientId, string remoteEndPoint, int activeCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Client {clientId} disconnected. Active: {activeCount}.")]
    private partial void LogClientDisconnected(string clientId, int activeCount);
}
