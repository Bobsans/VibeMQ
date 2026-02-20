using VibeMQ.Enums;

namespace VibeMQ.Configuration;

/// <summary>
/// Default settings applied to newly created queues.
/// </summary>
public sealed class QueueDefaults {
    /// <summary>
    /// Default delivery mode for new queues. Default: RoundRobin.
    /// </summary>
    public DeliveryMode DefaultDeliveryMode { get; set; } = DeliveryMode.RoundRobin;

    /// <summary>
    /// Maximum number of messages a queue can hold. Default: 10000.
    /// </summary>
    public int MaxQueueSize { get; set; } = 10_000;

    /// <summary>
    /// Whether queues are created automatically on first publish/subscribe. Default: true.
    /// </summary>
    public bool EnableAutoCreate { get; set; } = true;
}
