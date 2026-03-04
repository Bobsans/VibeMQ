using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VibeMQ.Models;

namespace VibeMQ.Server.Delivery;

/// <summary>
/// Tracks unacknowledged message deliveries, handles retry with exponential backoff,
/// and escalates to DLQ when max retries are exhausted.
/// </summary>
public sealed partial class AckTracker(
    TimeSpan? ackTimeout = null,
    TimeSpan? baseRetryDelay = null,
    TimeSpan? maxRetryDelay = null,
    ILogger<AckTracker>? logger = null
) : IAsyncDisposable {
    private readonly ConcurrentDictionary<string, PendingDelivery> _pending = new();
    private readonly TimeSpan _ackTimeout = ackTimeout ?? TimeSpan.FromSeconds(30);
    private readonly TimeSpan _baseRetryDelay = baseRetryDelay ?? TimeSpan.FromSeconds(2);
    private readonly TimeSpan _maxRetryDelay = maxRetryDelay ?? TimeSpan.FromMinutes(2);
    private readonly ILogger<AckTracker> _logger = logger ?? NullLogger<AckTracker>.Instance;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;

    /// <summary>
    /// Fired when a message has exhausted all retry attempts and should be moved to DLQ.
    /// </summary>
    public event Func<BrokerMessage, Task>? OnMessageExpired;

    /// <summary>
    /// Fired when a message needs to be redelivered to a subscriber.
    /// </summary>
    public event Func<PendingDelivery, Task>? OnRetryRequired;

    /// <summary>
    /// Number of currently pending (unacknowledged) deliveries.
    /// </summary>
    public int PendingCount => _pending.Count;

    /// <summary>
    /// Starts the background monitoring loop.
    /// </summary>
    public void Start() {
        _cts = new CancellationTokenSource();
        _monitorTask = MonitorLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Registers a message delivery for acknowledgment tracking.
    /// </summary>
    public void Track(BrokerMessage message, string clientId, int maxRetryAttempts = 3) {
        var delivery = new PendingDelivery(message, clientId) {
            NextRetryAt = DateTime.UtcNow.Add(_ackTimeout),
            MaxRetryAttempts = maxRetryAttempts,
        };

        _pending.TryAdd(message.Id, delivery);
        LogTracking(message.Id, clientId);
    }

    /// <summary>
    /// Acknowledges a message, removing it from tracking.
    /// Returns true if the message was found and removed.
    /// </summary>
    public bool Acknowledge(string messageId) {
        if (_pending.TryRemove(messageId, out _)) {
            LogAcknowledged(messageId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a message is currently being tracked (duplicate ACK protection).
    /// </summary>
    public bool IsTracked(string messageId) {
        return _pending.ContainsKey(messageId);
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            try {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                await ProcessPendingAsync().ConfigureAwait(false);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                LogMonitorError(ex);
            }
        }
    }

    private async Task ProcessPendingAsync() {
        var now = DateTime.UtcNow;

        foreach (var (messageId, delivery) in _pending) {
            if (now < delivery.NextRetryAt) {
                continue;
            }

            if (delivery.Attempts >= delivery.MaxRetryAttempts) {
                // Max retries exhausted — move to DLQ
                if (_pending.TryRemove(messageId, out _)) {
                    LogMaxRetriesExhausted(messageId, delivery.Attempts);

                    if (OnMessageExpired is not null) {
                        await OnMessageExpired(delivery.Message).ConfigureAwait(false);
                    }
                }

                continue;
            }

            // Schedule retry with exponential backoff
            delivery.Attempts++;
            delivery.NextRetryAt = now.Add(CalculateBackoff(delivery.Attempts));

            LogRetrying(messageId, delivery.Attempts, delivery.ClientId);

            if (OnRetryRequired is not null) {
                await OnRetryRequired(delivery).ConfigureAwait(false);
            }
        }
    }

    private TimeSpan CalculateBackoff(int attempt) {
        // Clamp exponent to avoid overflow (2^30 ticks × base delay is more than enough)
        var power = Math.Min(attempt - 1, 30);
        var delay = TimeSpan.FromTicks(
            _baseRetryDelay.Ticks * (1L << power)
        );

        return delay > _maxRetryDelay ? _maxRetryDelay : delay;
    }

    public async ValueTask DisposeAsync() {
        if (_cts is null || _cts.IsCancellationRequested) {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);

        if (_monitorTask is not null) {
            try {
                await _monitorTask.ConfigureAwait(false);
            } catch (OperationCanceledException) {
                // Expected
            }
        }

        _cts.Dispose();
        _cts = null;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tracking message {messageId} delivered to client {clientId}.")]
    private partial void LogTracking(string messageId, string clientId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Message {messageId} acknowledged.")]
    private partial void LogAcknowledged(string messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message {messageId} exhausted max retries ({attempts}). Moving to DLQ.")]
    private partial void LogMaxRetriesExhausted(string messageId, int attempts);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Retrying message {messageId} (attempt {attempt}) to client {clientId}.")]
    private partial void LogRetrying(string messageId, int attempt, string clientId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error in ack monitor loop.")]
    private partial void LogMonitorError(Exception exception);
}
