namespace VibeMQ.Core.Enums;

/// <summary>
/// Reason why a message was moved to the Dead Letter Queue.
/// </summary>
public enum FailureReason {
    MaxRetriesExceeded = 0,
    MessageExpired = 1,
    DeserializationError = 2,
    HandlerException = 3,
}
