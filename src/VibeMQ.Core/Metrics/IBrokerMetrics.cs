namespace VibeMQ.Core.Metrics;

/// <summary>
/// Interface for collecting broker-wide metrics.
/// All operations are thread-safe.
/// </summary>
public interface IBrokerMetrics {
    // --- Counters ---

    /// <summary>Total messages published.</summary>
    long TotalMessagesPublished { get; }

    /// <summary>Total messages delivered to subscribers.</summary>
    long TotalMessagesDelivered { get; }

    /// <summary>Total messages acknowledged by subscribers.</summary>
    long TotalMessagesAcknowledged { get; }

    /// <summary>Total delivery retries performed.</summary>
    long TotalRetries { get; }

    /// <summary>Total messages moved to Dead Letter Queue.</summary>
    long TotalDeadLettered { get; }

    /// <summary>Total errors encountered.</summary>
    long TotalErrors { get; }

    /// <summary>Total connections accepted.</summary>
    long TotalConnectionsAccepted { get; }

    /// <summary>Total connections rejected (rate-limited or over limit).</summary>
    long TotalConnectionsRejected { get; }

    // --- Gauges ---

    /// <summary>Current active connection count.</summary>
    int ActiveConnections { get; }

    /// <summary>Current active queue count.</summary>
    int ActiveQueues { get; }

    /// <summary>Current in-flight (unacknowledged) messages.</summary>
    int InFlightMessages { get; }

    /// <summary>Current process memory usage in bytes.</summary>
    long MemoryUsageBytes { get; }

    // --- Latency ---

    /// <summary>Average delivery latency in milliseconds.</summary>
    double AverageDeliveryLatencyMs { get; }

    // --- Recording methods ---

    void RecordPublished();
    void RecordDelivered(TimeSpan latency);
    void RecordAcknowledged();
    void RecordRetry();
    void RecordDeadLettered();
    void RecordError();
    void RecordConnectionAccepted();
    void RecordConnectionRejected();
    void UpdateGauges(int activeConnections, int activeQueues, int inFlightMessages);

    /// <summary>
    /// Returns a snapshot of all metrics.
    /// </summary>
    MetricsSnapshot GetSnapshot();
}
