using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using VibeMQ.Enums;
using VibeMQ.Interfaces;
using VibeMQ.Models;

namespace VibeMQ.Server.Delivery;

/// <summary>
/// Stores messages that failed delivery after all retries are exhausted.
/// Supports retrieval and manual retry of dead-lettered messages.
/// Persists entries via <see cref="IStorageProvider"/>.
/// </summary>
public sealed partial class DeadLetterQueue(IStorageProvider storageProvider, ILogger<DeadLetterQueue> logger) {
    private readonly ILogger<DeadLetterQueue> _logger = logger;
    private readonly ConcurrentQueue<DeadLetteredMessage> _messages = new();

    /// <summary>
    /// Number of messages in the DLQ.
    /// </summary>
    public int Count => _messages.Count;

    /// <summary>
    /// Adds a failed message to the Dead Letter Queue.
    /// </summary>
    public async Task HandleFailedMessageAsync(BrokerMessage message, FailureReason reason) {
        var dlqMessage = new DeadLetteredMessage {
            OriginalMessage = message,
            Reason = reason,
            FailedAt = DateTime.UtcNow,
        };

        await storageProvider.SaveDeadLetteredMessageAsync(dlqMessage).ConfigureAwait(false);
        _messages.Enqueue(dlqMessage);
        LogMessageDeadLettered(message.Id, message.QueueName, reason);
    }

    /// <summary>
    /// Retrieves up to <paramref name="count"/> messages from the DLQ without removing them.
    /// </summary>
    public Task<IReadOnlyList<DeadLetteredMessage>> GetMessagesAsync(int count) {
        IReadOnlyList<DeadLetteredMessage> result = _messages.ToArray()
            .Take(count)
            .ToArray();

        return Task.FromResult(result);
    }

    /// <summary>
    /// Dequeues a message from the DLQ for retry. Returns null if empty.
    /// </summary>
    public Task<DeadLetteredMessage?> DequeueAsync() {
        return Task.FromResult(_messages.TryDequeue(out var message) ? message : null);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message {messageId} from queue {queueName} moved to DLQ. Reason: {reason}.")]
    private partial void LogMessageDeadLettered(string messageId, string queueName, FailureReason reason);
}
