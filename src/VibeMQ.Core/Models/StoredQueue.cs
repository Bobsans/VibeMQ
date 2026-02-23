using VibeMQ.Configuration;

namespace VibeMQ.Models;

/// <summary>
/// Represents a persisted queue with its metadata and configuration.
/// </summary>
public sealed class StoredQueue {
    /// <summary>
    /// Queue name (unique identifier).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Configuration options for this queue.
    /// </summary>
    public required QueueOptions Options { get; init; }

    /// <summary>
    /// UTC timestamp when the queue was originally created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}
