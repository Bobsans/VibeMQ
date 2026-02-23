namespace VibeMQ.Models;

/// <summary>
/// Current storage statistics snapshot.
/// </summary>
public sealed class StorageStats {
    /// <summary>
    /// Total number of messages across all queues.
    /// </summary>
    public long TotalMessages { get; init; }

    /// <summary>
    /// Total number of queues.
    /// </summary>
    public long TotalQueues { get; init; }

    /// <summary>
    /// Total number of dead-lettered messages.
    /// </summary>
    public long TotalDeadLettered { get; init; }

    /// <summary>
    /// Storage size in bytes (file size, memory usage, etc.).
    /// </summary>
    public long StorageSizeBytes { get; init; }
}
