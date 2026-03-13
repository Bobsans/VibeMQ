namespace VibeMQ.Interfaces;

/// <summary>
/// Optional storage extension for bulk message operations.
/// </summary>
public interface IBulkMessageStorage {
    /// <summary>
    /// Removes many messages by id.
    /// </summary>
    Task RemoveMessagesAsync(
        IReadOnlyList<string> messageIds,
        CancellationToken cancellationToken = default);
}
