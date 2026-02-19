using System.Diagnostics;

namespace VibeMQ.Health;

/// <summary>
/// Snapshot of the broker's health status, returned by the /health/ endpoint.
/// </summary>
public sealed class HealthStatus {
    public required bool IsHealthy { get; init; }
    public required string Status { get; init; }
    public int ActiveConnections { get; init; }
    public int QueueCount { get; init; }
    public int InFlightMessages { get; init; }
    public long TotalMessagesPublished { get; init; }
    public long TotalMessagesDelivered { get; init; }
    public long MemoryUsageMb { get; init; } = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
