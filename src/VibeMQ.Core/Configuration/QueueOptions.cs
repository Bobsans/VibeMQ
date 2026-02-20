using VibeMQ.Enums;

namespace VibeMQ.Configuration;

/// <summary>
/// Configuration options for an individual queue.
/// </summary>
public sealed class QueueOptions {
    /// <summary>
    /// Delivery mode for this queue.
    /// </summary>
    public DeliveryMode Mode { get; set; } = DeliveryMode.RoundRobin;

    /// <summary>
    /// Maximum number of messages the queue can hold.
    /// </summary>
    public int MaxQueueSize { get; set; } = 10_000;

    /// <summary>
    /// Optional time-to-live for messages. Expired messages are discarded or moved to DLQ.
    /// </summary>
    public TimeSpan? MessageTtl { get; set; }

    /// <summary>
    /// Whether a Dead Letter Queue is enabled for this queue.
    /// </summary>
    public bool EnableDeadLetterQueue { get; set; }

    /// <summary>
    /// Name of the Dead Letter Queue. Auto-generated if not specified.
    /// </summary>
    public string? DeadLetterQueueName { get; set; }

    /// <summary>
    /// Strategy to apply when the queue reaches its maximum size.
    /// </summary>
    public OverflowStrategy OverflowStrategy { get; set; } = OverflowStrategy.DropOldest;

    /// <summary>
    /// Maximum number of delivery retry attempts before moving to DLQ. Default: 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
