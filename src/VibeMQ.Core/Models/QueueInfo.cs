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

    /// <summary>
    /// Optional time-to-live for messages. Null means messages never expire.
    /// </summary>
    public TimeSpan? MessageTtl { get; init; }

    /// <summary>
    /// Whether a Dead Letter Queue is enabled for this queue.
    /// </summary>
    public bool EnableDeadLetterQueue { get; init; }

    /// <summary>
    /// Name of the Dead Letter Queue. Null if DLQ is not enabled or name was auto-generated.
    /// </summary>
    public string? DeadLetterQueueName { get; init; }

    /// <summary>
    /// Strategy applied when the queue reaches its maximum size.
    /// </summary>
    public OverflowStrategy OverflowStrategy { get; init; }

    /// <summary>
    /// Maximum number of delivery retry attempts before moving to DLQ.
    /// </summary>
    public int MaxRetryAttempts { get; init; }
}
