using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Core.Configuration;
using VibeMQ.Core.Enums;
using VibeMQ.Core.Interfaces;
using VibeMQ.Core.Models;
using VibeMQ.Protocol;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Queues;

/// <summary>
/// Manages all message queues: creation, publishing, delivery, and acknowledgment.
/// </summary>
public sealed partial class QueueManager : IQueueManager {
    private readonly ConcurrentDictionary<string, MessageQueue> _queues = new();
    private readonly ConnectionManager _connectionManager;
    private readonly QueueDefaults _defaults;
    private readonly ILogger<QueueManager> _logger;

    public QueueManager(
        ConnectionManager connectionManager,
        QueueDefaults defaults,
        ILogger<QueueManager> logger
    ) {
        _connectionManager = connectionManager;
        _defaults = defaults;
        _logger = logger;
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
            // Auto-create queue if enabled
            if (_defaults.EnableAutoCreate) {
                await CreateQueueAsync(message.QueueName, cancellationToken: cancellationToken).ConfigureAwait(false);
                _queues.TryGetValue(message.QueueName, out queue);
            }

            if (queue is null) {
                LogQueueNotFound(message.QueueName);
                return;
            }
        }

        var accepted = queue.Enqueue(message);

        if (!accepted) {
            LogMessageRejected(message.Id, message.QueueName, queue.Options.OverflowStrategy);
            return;
        }

        // Deliver to subscribers immediately
        await DeliverAsync(queue, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> AcknowledgeAsync(string messageId, CancellationToken cancellationToken = default) {
        foreach (var queue in _queues.Values) {
            if (queue.Acknowledge(messageId)) {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Delivers pending messages from a queue to its subscribers based on the delivery mode.
    /// </summary>
    private async Task DeliverAsync(MessageQueue queue, CancellationToken cancellationToken) {
        var subscribers = _connectionManager.GetSubscribers(queue.Name);

        if (subscribers.Count == 0) {
            return;
        }

        switch (queue.Options.Mode) {
            case DeliveryMode.RoundRobin:
                await DeliverRoundRobinAsync(queue, subscribers, cancellationToken).ConfigureAwait(false);
                break;

            case DeliveryMode.FanOutWithAck:
                await DeliverFanOutAsync(queue, subscribers, requireAck: true, cancellationToken).ConfigureAwait(false);
                break;

            case DeliveryMode.FanOutWithoutAck:
                await DeliverFanOutAsync(queue, subscribers, requireAck: false, cancellationToken).ConfigureAwait(false);
                break;

            case DeliveryMode.PriorityBased:
                await DeliverRoundRobinAsync(queue, subscribers, cancellationToken).ConfigureAwait(false);
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

            if (queue.Options.Mode is DeliveryMode.RoundRobin or DeliveryMode.PriorityBased) {
                queue.TrackUnacknowledged(message);
            }

            LogMessageDelivered(message.Id, queue.Name, target.Id);
        } catch (Exception ex) {
            LogDeliveryFailed(message.Id, target.Id, ex);
            // Re-enqueue for retry
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
                    queue.TrackUnacknowledged(message);
                }

                LogMessageDelivered(message.Id, queue.Name, subscriber.Id);
            } catch (Exception ex) {
                LogDeliveryFailed(message.Id, subscriber.Id, ex);
            }
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
