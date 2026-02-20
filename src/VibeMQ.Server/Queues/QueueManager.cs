using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Configuration;
using VibeMQ.Enums;
using VibeMQ.Interfaces;
using VibeMQ.Models;
using VibeMQ.Protocol;
using VibeMQ.Metrics;
using VibeMQ.Server.Connections;
using VibeMQ.Server.Delivery;

namespace VibeMQ.Server.Queues;

/// <summary>
/// Manages all message queues: creation, publishing, delivery, acknowledgment, DLQ.
/// Integrates with <see cref="AckTracker"/> for retry logic and <see cref="DeadLetterQueue"/> for failed messages.
/// </summary>
public sealed partial class QueueManager : IQueueManager {
    private readonly ConcurrentDictionary<string, MessageQueue> _queues = new();
    private readonly ConnectionManager _connectionManager;
    private readonly AckTracker _ackTracker;
    private readonly DeadLetterQueue _deadLetterQueue;
    private readonly IBrokerMetrics _metrics;
    private readonly QueueDefaults _defaults;
    private readonly ILogger<QueueManager> _logger;

    public QueueManager(
        ConnectionManager connectionManager,
        AckTracker ackTracker,
        DeadLetterQueue deadLetterQueue,
        IBrokerMetrics metrics,
        QueueDefaults defaults,
        ILogger<QueueManager> logger
    ) {
        _connectionManager = connectionManager;
        _ackTracker = ackTracker;
        _deadLetterQueue = deadLetterQueue;
        _metrics = metrics;
        _defaults = defaults;
        _logger = logger;

        // Wire up AckTracker events
        _ackTracker.OnMessageExpired += OnMessageExpiredAsync;
        _ackTracker.OnRetryRequired += OnRetryRequiredAsync;
    }

    /// <inheritdoc />
    public int QueueCount => _queues.Count;

    /// <inheritdoc />
    public Task CreateQueueAsync(string name, QueueOptions? options = null, CancellationToken cancellationToken = default) {
        var queueOptions = options ?? new QueueOptions {
            Mode = _defaults.DefaultDeliveryMode,
            MaxQueueSize = _defaults.MaxQueueSize,
        };

        var queue = new MessageQueue(name, queueOptions);

        if (_queues.TryAdd(name, queue)) {
            LogQueueCreated(name, queueOptions.Mode);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteQueueAsync(string name, CancellationToken cancellationToken = default) {
        if (_queues.TryRemove(name, out _)) {
            LogQueueDeleted(name);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<QueueInfo?> GetQueueInfoAsync(string name, CancellationToken cancellationToken = default) {
        if (!_queues.TryGetValue(name, out var queue)) {
            return Task.FromResult<QueueInfo?>(null);
        }

        var subscriberCount = _connectionManager.GetSubscribers(name).Count;
        return Task.FromResult<QueueInfo?>(queue.ToQueueInfo(subscriberCount));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListQueuesAsync(CancellationToken cancellationToken = default) {
        IReadOnlyList<string> names = _queues.Keys.ToArray();
        return Task.FromResult(names);
    }

    /// <inheritdoc />
    public async Task PublishAsync(BrokerMessage message, CancellationToken cancellationToken = default) {
        if (!_queues.TryGetValue(message.QueueName, out var queue)) {
            if (_defaults.EnableAutoCreate) {
                await CreateQueueAsync(message.QueueName, cancellationToken: cancellationToken).ConfigureAwait(false);
                _queues.TryGetValue(message.QueueName, out queue);
            }

            if (queue is null) {
                LogQueueNotFound(message.QueueName);
                return;
            }
        }

        // Set max retry attempts from queue config
        message.DeliveryAttempts = queue.Options.MaxRetryAttempts;

        var accepted = queue.Enqueue(message);

        if (!accepted) {
            LogMessageRejected(message.Id, message.QueueName, queue.Options.OverflowStrategy);

            // If overflow strategy is RedirectToDlq, send to DLQ
            if (queue.Options.OverflowStrategy == OverflowStrategy.RedirectToDlq && queue.Options.EnableDeadLetterQueue) {
                await _deadLetterQueue.HandleFailedMessageAsync(message, FailureReason.MaxRetriesExceeded)
                    .ConfigureAwait(false);
                _metrics.RecordDeadLettered();
            }

            _metrics.RecordError();
            return;
        }

        _metrics.RecordPublished();
        await DeliverAsync(queue, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> AcknowledgeAsync(string messageId, CancellationToken cancellationToken = default) {
        // First try the AckTracker (centralized tracking)
        if (_ackTracker.Acknowledge(messageId)) {
            _metrics.RecordAcknowledged();
            return Task.FromResult(true);
        }

        // Fallback: check individual queues
        foreach (var queue in _queues.Values) {
            if (queue.Acknowledge(messageId)) {
                _metrics.RecordAcknowledged();
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    private async Task DeliverAsync(MessageQueue queue, CancellationToken cancellationToken) {
        var subscribers = _connectionManager.GetSubscribers(queue.Name);

        if (subscribers.Count == 0) {
            return;
        }

        switch (queue.Options.Mode) {
            case DeliveryMode.RoundRobin:
            case DeliveryMode.PriorityBased:
                await DeliverRoundRobinAsync(queue, subscribers, cancellationToken).ConfigureAwait(false);
                break;

            case DeliveryMode.FanOutWithAck:
                await DeliverFanOutAsync(queue, subscribers, requireAck: true, cancellationToken).ConfigureAwait(false);
                break;

            case DeliveryMode.FanOutWithoutAck:
                await DeliverFanOutAsync(queue, subscribers, requireAck: false, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task DeliverRoundRobinAsync(
        MessageQueue queue,
        IReadOnlyList<ClientConnection> subscribers,
        CancellationToken cancellationToken
    ) {
        var message = queue.Dequeue();

        if (message is null) {
            return;
        }

        var index = queue.GetNextRoundRobinIndex(subscribers.Count);
        var target = subscribers[index];
        var protocolMessage = ToDeliverMessage(message);

        try {
            await target.SendMessageAsync(protocolMessage, cancellationToken).ConfigureAwait(false);
            _ackTracker.Track(message, target.Id);
            _metrics.RecordDelivered(DateTime.UtcNow - message.Timestamp);
            LogMessageDelivered(message.Id, queue.Name, target.Id);
        } catch (Exception ex) {
            _metrics.RecordError();
            LogDeliveryFailed(message.Id, target.Id, ex);
            queue.Enqueue(message);
        }
    }

    private async Task DeliverFanOutAsync(
        MessageQueue queue,
        IReadOnlyList<ClientConnection> subscribers,
        bool requireAck,
        CancellationToken cancellationToken
    ) {
        var message = queue.Dequeue();

        if (message is null) {
            return;
        }

        var protocolMessage = ToDeliverMessage(message);

        foreach (var subscriber in subscribers) {
            try {
                await subscriber.SendMessageAsync(protocolMessage, cancellationToken).ConfigureAwait(false);

                if (requireAck) {
                    _ackTracker.Track(message, subscriber.Id);
                }

                _metrics.RecordDelivered(DateTime.UtcNow - message.Timestamp);
                LogMessageDelivered(message.Id, queue.Name, subscriber.Id);
            } catch (Exception ex) {
                _metrics.RecordError();
                LogDeliveryFailed(message.Id, subscriber.Id, ex);
            }
        }
    }

    /// <summary>
    /// Called by AckTracker when a message has exhausted all retries.
    /// </summary>
    private async Task OnMessageExpiredAsync(BrokerMessage message) {
        if (_queues.TryGetValue(message.QueueName, out var queue) && queue.Options.EnableDeadLetterQueue) {
            await _deadLetterQueue.HandleFailedMessageAsync(message, FailureReason.MaxRetriesExceeded)
                .ConfigureAwait(false);
            _metrics.RecordDeadLettered();
        }
    }

    /// <summary>
    /// Called by AckTracker when a message needs to be redelivered.
    /// </summary>
    private async Task OnRetryRequiredAsync(PendingDelivery delivery) {
        _metrics.RecordRetry();
        var connection = _connectionManager.Get(delivery.ClientId);

        if (connection is null || !connection.IsConnected) {
            // Original subscriber is gone — re-enqueue for another subscriber
            if (_queues.TryGetValue(delivery.Message.QueueName, out var queue)) {
                queue.Enqueue(delivery.Message);
            }

            return;
        }

        var protocolMessage = ToDeliverMessage(delivery.Message);

        try {
            await connection.SendMessageAsync(protocolMessage, CancellationToken.None).ConfigureAwait(false);
        } catch {
            // If redelivery fails, the message stays in AckTracker for next retry cycle
        }
    }

    private static ProtocolMessage ToDeliverMessage(BrokerMessage message) {
        return new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.Deliver,
            Queue = message.QueueName,
            Payload = message.Payload.ValueKind != JsonValueKind.Undefined ? message.Payload : null,
            Headers = message.Headers.Count > 0 ? message.Headers : null,
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Queue {queueName} created with delivery mode {deliveryMode}.")]
    private partial void LogQueueCreated(string queueName, DeliveryMode deliveryMode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Queue {queueName} deleted.")]
    private partial void LogQueueDeleted(string queueName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Queue {queueName} not found.")]
    private partial void LogQueueNotFound(string queueName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message {messageId} rejected from queue {queueName} (overflow: {strategy}).")]
    private partial void LogMessageRejected(string messageId, string queueName, OverflowStrategy strategy);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Message {messageId} delivered from {queueName} to client {clientId}.")]
    private partial void LogMessageDelivered(string messageId, string queueName, string clientId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deliver message {messageId} to client {clientId}.")]
    private partial void LogDeliveryFailed(string messageId, string clientId, Exception exception);
}
