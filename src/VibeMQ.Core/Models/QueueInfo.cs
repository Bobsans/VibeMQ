using VibeMQ.Enums;

namespace VibeMQ.Models;

/// <summary>
/// Read-only snapshot of queue state.
/// </summary>
public sealed class QueueInfo {
    /// <summary>
    /// Queue name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Current number of messages in the queue.
    /// </summary>
    public int MessageCount { get; init; }

    /// <summary>
    /// Number of active subscribers.
    /// </summary>
    public int SubscriberCount { get; init; }

    /// <summary>
    /// Delivery mode configured for this queue.
    /// </summary>
    public DeliveryMode DeliveryMode { get; init; }

    /// <summary>
    /// Maximum allowed queue size.
    /// </summary>
    public int MaxSize { get; init; }

    /// <summary>
    /// Timestamp when the queue was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }
}
