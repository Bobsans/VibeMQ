using VibeMQ.Configuration;

namespace VibeMQ.Client;

/// <summary>
/// Describes a queue that the client will automatically create or verify on connection.
/// </summary>
public sealed class QueueDeclaration {
    /// <summary>
    /// Name of the queue.
    /// </summary>
    public required string QueueName { get; init; }

    /// <summary>
    /// Desired queue settings.
    /// </summary>
    public QueueOptions Options { get; init; } = new();

    /// <summary>
    /// Strategy applied when there is at least one <see cref="ConflictSeverity.Soft"/> or
    /// <see cref="ConflictSeverity.Hard"/> difference between the declared settings and the
    /// existing queue. <see cref="ConflictSeverity.Info"/> differences are never treated as conflicts.
    /// Default: <see cref="QueueConflictResolution.Ignore"/>.
    /// </summary>
    public QueueConflictResolution OnConflict { get; init; } = QueueConflictResolution.Ignore;

    /// <summary>
    /// When <c>true</c> (default), a provisioning error aborts <c>ConnectAsync</c>.
    /// When <c>false</c>, the error is logged and provisioning continues with the next declaration.
    /// Does not affect conflict handling; use <see cref="OnConflict"/> for that.
    /// </summary>
    public bool FailOnProvisioningError { get; init; } = true;
}
