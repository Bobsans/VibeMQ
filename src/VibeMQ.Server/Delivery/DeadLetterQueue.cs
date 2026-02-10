using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using VibeMQ.Core.Enums;
using VibeMQ.Core.Models;

namespace VibeMQ.Server.Delivery;

/// <summary>
/// Stores messages that failed delivery after all retries are exhausted.
/// Supports retrieval and manual retry of dead-lettered messages.
/// </summary>
public sealed partial class DeadLetterQueue {
    private readonly ConcurrentQueue<DeadLetteredMessage> _messages = new();
    private readonly ILogger<DeadLetterQueue> _logger;

    public DeadLetterQueue(ILogger<DeadLetterQueue> logger) {
        _logger = logger;
    }

    /// <summary>
    /// Number of messages in the DLQ.
    /// </summary>
    public int Count => _messages.Count;

    /// <summary>
    /// Adds a failed message to the Dead Letter Queue.
    /// </summary>
    public Task HandleFailedMessageAsync(BrokerMessage message, FailureReason reason) {
        var dlqMessage = new DeadLetteredMessage {
            OriginalMessage = message,
            Reason = reason,
            FailedAt = DateTime.UtcNow,
        };

        _messages.Enqueue(dlqMessage);
        LogMessageDeadLettered(message.Id, message.QueueName, reason);

        return Task.CompletedTask;
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
