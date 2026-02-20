namespace VibeMQ.Metrics;

/// <summary>
/// Immutable point-in-time snapshot of all broker metrics.
/// </summary>
public sealed class MetricsSnapshot {
    // Counters
    public long TotalMessagesPublished { get; init; }
    public long TotalMessagesDelivered { get; init; }
    public long TotalMessagesAcknowledged { get; init; }
    public long TotalRetries { get; init; }
    public long TotalDeadLettered { get; init; }
    public long TotalErrors { get; init; }
    public long TotalConnectionsAccepted { get; init; }
    public long TotalConnectionsRejected { get; init; }

    // Gauges
    public int ActiveConnections { get; init; }
    public int ActiveQueues { get; init; }
    public int InFlightMessages { get; init; }
    public long MemoryUsageBytes { get; init; }

    // Latency
    public double AverageDeliveryLatencyMs { get; init; }

    // Metadata
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public TimeSpan Uptime { get; init; }
}
