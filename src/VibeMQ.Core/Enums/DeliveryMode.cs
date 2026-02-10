namespace VibeMQ.Core.Enums;

/// <summary>
/// Determines how messages are delivered to subscribers of a queue.
/// </summary>
public enum DeliveryMode {
    /// <summary>
    /// Delivers each message to a single subscriber in round-robin fashion.
    /// </summary>
    RoundRobin = 0,

    /// <summary>
    /// Delivers each message to all subscribers, requiring acknowledgment from each.
    /// </summary>
    FanOutWithAck = 1,

    /// <summary>
    /// Delivers each message to all subscribers without requiring acknowledgment.
    /// </summary>
    FanOutWithoutAck = 2,

    /// <summary>
    /// Delivers messages based on their priority level.
    /// </summary>
    PriorityBased = 3,
}
