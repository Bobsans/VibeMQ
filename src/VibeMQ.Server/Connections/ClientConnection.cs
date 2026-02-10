using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using VibeMQ.Protocol;
using VibeMQ.Protocol.Framing;

namespace VibeMQ.Server.Connections;

/// <summary>
/// Represents a single client connection to the broker.
/// Wraps a <see cref="TcpClient"/> with frame-level read/write operations.
/// Supports both plain TCP and TLS (via <see cref="Stream"/> abstraction).
/// </summary>
public sealed partial class ClientConnection : IAsyncDisposable {
    private readonly TcpClient _tcpClient;
    private Stream _stream;
    private readonly ILogger _logger;
    private readonly int _maxMessageSize;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private int _disposed;

    public ClientConnection(
        string id,
        TcpClient tcpClient,
        int maxMessageSize,
        ILogger logger
    ) {
        Id = id;
        _tcpClient = tcpClient;
        _stream = tcpClient.GetStream();
        _maxMessageSize = maxMessageSize;
        _logger = logger;
        RemoteEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
        ConnectedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Unique connection identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Whether this connection has been authenticated.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Remote endpoint of the client.
    /// </summary>
    public IPEndPoint? RemoteEndPoint { get; }

    /// <summary>
    /// UTC timestamp when the connection was established.
    /// </summary>
    public DateTime ConnectedAt { get; }

    /// <summary>
    /// UTC timestamp of the last activity (message sent or received).
    /// </summary>
    public DateTime LastActivity { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Set of queue names this client is subscribed to.
    /// </summary>
    public HashSet<string> Subscriptions { get; } = [];

    /// <summary>
    /// Whether the underlying TCP connection is still open.
    /// </summary>
    public bool IsConnected => _tcpClient.Connected && Volatile.Read(ref _disposed) == 0;

    /// <summary>
    /// Upgrades the connection stream to TLS.
    /// Must be called before any read/write operations.
    /// </summary>
    public void UpgradeToTls(Stream sslStream) {
        _stream = sslStream;
    }

    /// <summary>
    /// Reads the next protocol message from the connection.
    /// Returns null if the connection was closed gracefully.
    /// </summary>
    public async Task<ProtocolMessage?> ReadMessageAsync(CancellationToken cancellationToken = default) {
        var message = await FrameReader.ReadFrameAsync(_stream, _maxMessageSize, cancellationToken)
            .ConfigureAwait(false);

        if (message is not null) {
            LastActivity = DateTime.UtcNow;
        }

        return message;
    }

    /// <summary>
    /// Sends a protocol message to the client. Thread-safe via write lock.
    /// </summary>
    public async Task SendMessageAsync(ProtocolMessage message, CancellationToken cancellationToken = default) {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try {
            await FrameWriter.WriteFrameAsync(_stream, message, cancellationToken).ConfigureAwait(false);
            LastActivity = DateTime.UtcNow;
        } finally {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Sends an error response to the client.
    /// </summary>
    public Task SendErrorAsync(string errorCode, string errorMessage, CancellationToken cancellationToken = default) {
        return SendMessageAsync(new ProtocolMessage {
            Type = CommandType.Error,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
        }, cancellationToken);
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) {
            return;
        }

        LogDisconnecting(Id, RemoteEndPoint);

        try {
            await _stream.DisposeAsync().ConfigureAwait(false);
        } catch {
            // Ignore stream close errors
        }

        _tcpClient.Close();
        _writeLock.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {clientId} disconnecting ({remoteEndPoint}).")]
    private partial void LogDisconnecting(string clientId, IPEndPoint? remoteEndPoint);
}
