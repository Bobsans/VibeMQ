namespace VibeMQ.Client;

/// <summary>
/// Strategy for resolving conflicts between a declared queue configuration and an existing one.
/// A conflict exists when there is at least one <see cref="ConflictSeverity.Soft"/> or
/// <see cref="ConflictSeverity.Hard"/> difference. <see cref="ConflictSeverity.Info"/> differences
/// are never considered conflicts.
/// </summary>
public enum QueueConflictResolution {
    /// <summary>
    /// Leave the existing queue as-is.
    /// Info differences: Debug log. Soft differences: Warning log. Hard differences: Error log.
    /// Safe default for production.
    /// </summary>
    Ignore,

    /// <summary>
    /// Throw <see cref="Exceptions.QueueConflictException"/> if there is at least one Soft or Hard difference.
    /// Suitable for production environments where settings drift equals a deployment error.
    /// </summary>
    Fail,

    /// <summary>
    /// Delete the queue and recreate it with the declared settings when there is at least one Soft or Hard difference.
    /// <para><b>Warning:</b> all messages in the queue will be permanently lost.</para>
    /// </summary>
    Override
}
