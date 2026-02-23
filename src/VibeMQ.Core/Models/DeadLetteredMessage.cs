using VibeMQ.Enums;

namespace VibeMQ.Models;

/// <summary>
/// A message that was moved to the Dead Letter Queue after failing delivery.
/// </summary>
public sealed class DeadLetteredMessage {
    /// <summary>
    /// The original message that failed delivery.
    /// </summary>
    public required BrokerMessage OriginalMessage { get; init; }

    /// <summary>
    /// Reason the message was dead-lettered.
    /// </summary>
    public required FailureReason Reason { get; init; }

    /// <summary>
    /// UTC timestamp when the message was moved to DLQ.
    /// </summary>
    public required DateTime FailedAt { get; init; }
}
