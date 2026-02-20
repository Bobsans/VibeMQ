using System.Diagnostics;
using VibeMQ.Metrics;

namespace VibeMQ.Server.Metrics;

/// <summary>
/// Thread-safe implementation of <see cref="IBrokerMetrics"/>.
/// Uses <see cref="Interlocked"/> for lock-free counter increments.
/// </summary>
public sealed class BrokerMetrics : IBrokerMetrics {
    private readonly DateTime _startedAt = DateTime.UtcNow;

    // Counters (mutable via Interlocked)
    private long _totalMessagesPublished;
    private long _totalMessagesDelivered;
    private long _totalMessagesAcknowledged;
    private long _totalRetries;
    private long _totalDeadLettered;
    private long _totalErrors;
    private long _totalConnectionsAccepted;
    private long _totalConnectionsRejected;

    // Gauges (updated periodically)
    private int _activeConnections;
    private int _activeQueues;
    private int _inFlightMessages;

    // Latency tracking
    private long _totalDeliveryLatencyTicks;
    private long _deliveryCount;

    // --- Counter properties ---

    public long TotalMessagesPublished => Volatile.Read(ref _totalMessagesPublished);
    public long TotalMessagesDelivered => Volatile.Read(ref _totalMessagesDelivered);
    public long TotalMessagesAcknowledged => Volatile.Read(ref _totalMessagesAcknowledged);
    public long TotalRetries => Volatile.Read(ref _totalRetries);
    public long TotalDeadLettered => Volatile.Read(ref _totalDeadLettered);
    public long TotalErrors => Volatile.Read(ref _totalErrors);
    public long TotalConnectionsAccepted => Volatile.Read(ref _totalConnectionsAccepted);
    public long TotalConnectionsRejected => Volatile.Read(ref _totalConnectionsRejected);

    // --- Gauge properties ---

    public int ActiveConnections => Volatile.Read(ref _activeConnections);
    public int ActiveQueues => Volatile.Read(ref _activeQueues);
    public int InFlightMessages => Volatile.Read(ref _inFlightMessages);

    public long MemoryUsageBytes => Process.GetCurrentProcess().WorkingSet64;

    // --- Latency ---

    public double AverageDeliveryLatencyMs {
        get {
            var count = Volatile.Read(ref _deliveryCount);

            if (count == 0) {
                return 0;
            }

            var ticks = Volatile.Read(ref _totalDeliveryLatencyTicks);
            return TimeSpan.FromTicks(ticks / count).TotalMilliseconds;
        }
    }

    // --- Recording methods ---

    public void RecordPublished() {
        Interlocked.Increment(ref _totalMessagesPublished);
    }

    public void RecordDelivered(TimeSpan latency) {
        Interlocked.Increment(ref _totalMessagesDelivered);
        Interlocked.Add(ref _totalDeliveryLatencyTicks, latency.Ticks);
        Interlocked.Increment(ref _deliveryCount);
    }

    public void RecordAcknowledged() {
        Interlocked.Increment(ref _totalMessagesAcknowledged);
    }

    public void RecordRetry() {
        Interlocked.Increment(ref _totalRetries);
    }

    public void RecordDeadLettered() {
        Interlocked.Increment(ref _totalDeadLettered);
    }

    public void RecordError() {
        Interlocked.Increment(ref _totalErrors);
    }

    public void RecordConnectionAccepted() {
        Interlocked.Increment(ref _totalConnectionsAccepted);
    }

    public void RecordConnectionRejected() {
        Interlocked.Increment(ref _totalConnectionsRejected);
    }

    public void UpdateGauges(int activeConnections, int activeQueues, int inFlightMessages) {
        Volatile.Write(ref _activeConnections, activeConnections);
        Volatile.Write(ref _activeQueues, activeQueues);
        Volatile.Write(ref _inFlightMessages, inFlightMessages);
    }

    public MetricsSnapshot GetSnapshot() {
        return new MetricsSnapshot {
            TotalMessagesPublished = TotalMessagesPublished,
            TotalMessagesDelivered = TotalMessagesDelivered,
            TotalMessagesAcknowledged = TotalMessagesAcknowledged,
            TotalRetries = TotalRetries,
            TotalDeadLettered = TotalDeadLettered,
            TotalErrors = TotalErrors,
            TotalConnectionsAccepted = TotalConnectionsAccepted,
            TotalConnectionsRejected = TotalConnectionsRejected,
            ActiveConnections = ActiveConnections,
            ActiveQueues = ActiveQueues,
            InFlightMessages = InFlightMessages,
            MemoryUsageBytes = MemoryUsageBytes,
            AverageDeliveryLatencyMs = AverageDeliveryLatencyMs,
            Timestamp = DateTime.UtcNow,
            Uptime = DateTime.UtcNow - _startedAt,
        };
    }
}
