namespace VibeMQ.Enums;

/// <summary>
/// Strategy for handling queue overflow when the maximum size is reached.
/// </summary>
public enum OverflowStrategy {
    /// <summary>
    /// Drops the oldest message in the queue to make room.
    /// </summary>
    DropOldest = 0,

    /// <summary>
    /// Rejects the new incoming message.
    /// </summary>
    DropNewest = 1,

    /// <summary>
    /// Blocks the publisher until space is available.
    /// </summary>
    BlockPublisher = 2,

    /// <summary>
    /// Redirects the message to the Dead Letter Queue.
    /// </summary>
    RedirectToDlq = 3,
}
