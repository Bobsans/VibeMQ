using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using VibeMQ.Configuration;
using VibeMQ.Interfaces;
using VibeMQ.Metrics;
using VibeMQ.Models;
using VibeMQ.Server.Connections;
using VibeMQ.Server.Delivery;
using VibeMQ.Server.Metrics;
using VibeMQ.Server.Queues;

namespace VibeMQ.Tests.Unit.Queues;

public sealed class QueueManagerStorageExtensionsTests {
    [Fact]
    public async Task InitializeAsync_RequeuesRecoveredInflightMessages() {
        var storage = new FakeStorageProvider {
            Queues = [
                new StoredQueue {
                    Name = "orders",
                    Options = new QueueOptions(),
                    CreatedAt = DateTime.UtcNow
                }
            ],
            RecoveredInFlight = [
                new BrokerMessage {
                    Id = "inflight-1",
                    QueueName = "orders",
                    Payload = JsonSerializer.SerializeToElement(new { value = 42 }),
                    Timestamp = DateTime.UtcNow
                }
            ]
        };

        var queueManager = CreateQueueManager(storage);
        await queueManager.InitializeAsync();

        var recovered = await queueManager.GetPendingMessagesForDashboardAsync("orders", 10, 0);
        Assert.Contains(recovered, m => m.Id == "inflight-1");
    }

    [Fact]
    public async Task PurgeQueueAsync_UsesBulkStorageWhenAvailable() {
        var storage = new FakeStorageProvider();
        var queueManager = CreateQueueManager(storage);
        await queueManager.InitializeAsync();
        await queueManager.CreateQueueAsync("bulk-queue");

        await queueManager.PublishAsync(new BrokerMessage {
            Id = "m1",
            QueueName = "bulk-queue",
            Payload = JsonSerializer.SerializeToElement("one"),
            Timestamp = DateTime.UtcNow
        });
        await queueManager.PublishAsync(new BrokerMessage {
            Id = "m2",
            QueueName = "bulk-queue",
            Payload = JsonSerializer.SerializeToElement("two"),
            Timestamp = DateTime.UtcNow
        });

        var ok = await queueManager.PurgeQueueAsync("bulk-queue");

        Assert.True(ok);
        Assert.Equal(["m1", "m2"], [.. storage.BulkRemovedIds.OrderBy(x => x)]);
    }

    private static QueueManager CreateQueueManager(FakeStorageProvider storage) {
        IBrokerMetrics metrics = new BrokerMetrics();
        var connectionManager = new ConnectionManager(
            100,
            metrics,
            NullLogger<ConnectionManager>.Instance
        );
        var ackTracker = new AckTracker(logger: NullLogger<AckTracker>.Instance);
        var deadLetterQueue = new DeadLetterQueue(storage, NullLogger<DeadLetterQueue>.Instance);
        return new QueueManager(
            connectionManager,
            ackTracker,
            deadLetterQueue,
            storage,
            metrics,
            new QueueDefaults(),
            NullLogger<QueueManager>.Instance
        );
    }

    private sealed class FakeStorageProvider : IStorageProvider, IInFlightMessageStorage, IBulkMessageStorage {
        private readonly Dictionary<string, QueueOptions> _queueOptions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, BrokerMessage> _messages = new(StringComparer.Ordinal);

        public IReadOnlyList<StoredQueue> Queues { get; set; } = [];
        public IReadOnlyList<BrokerMessage> RecoveredInFlight { get; set; } = [];
        public List<string> BulkRemovedIds { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task SaveMessageAsync(BrokerMessage message, CancellationToken cancellationToken = default) {
            _messages[message.Id] = message;
            return Task.CompletedTask;
        }

        public Task<BrokerMessage?> GetMessageAsync(string id, CancellationToken cancellationToken = default) {
            _messages.TryGetValue(id, out var message);
            return Task.FromResult(message);
        }

        public Task<bool> RemoveMessageAsync(string id, CancellationToken cancellationToken = default) {
            return Task.FromResult(_messages.Remove(id));
        }

        public Task<IReadOnlyList<BrokerMessage>> GetPendingMessagesAsync(string queueName, CancellationToken cancellationToken = default) {
            var pending = _messages.Values
                .Where(m => m.QueueName == queueName)
                .OrderBy(m => m.Timestamp)
                .ToArray();
            return Task.FromResult<IReadOnlyList<BrokerMessage>>(pending);
        }

        public Task SaveQueueAsync(string name, QueueOptions queueOptions, CancellationToken cancellationToken = default) {
            _queueOptions[name] = queueOptions;
            return Task.CompletedTask;
        }

        public Task RemoveQueueAsync(string name, CancellationToken cancellationToken = default) {
            _queueOptions.Remove(name);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredQueue>> GetAllQueuesAsync(CancellationToken cancellationToken = default) {
            if (Queues.Count > 0) {
                return Task.FromResult(Queues);
            }

            var list = _queueOptions.Select(x => new StoredQueue {
                Name = x.Key,
                Options = x.Value,
                CreatedAt = DateTime.UtcNow
            }).ToArray();
            return Task.FromResult<IReadOnlyList<StoredQueue>>(list);
        }

        public Task SaveDeadLetteredMessageAsync(DeadLetteredMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DeadLetteredMessage>> GetDeadLetteredMessagesAsync(int count, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeadLetteredMessage>>([]);
        public Task<bool> RemoveDeadLetteredMessageAsync(string messageId, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task MarkMessageInFlightAsync(BrokerMessage message, string clientId, int maxRetryAttempts, DateTime nextRetryAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateInFlightRetryAsync(string messageId, int deliveryAttempts, DateTime nextRetryAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RequeueInFlightMessageAsync(string messageId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveInFlightStateAsync(string messageId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<BrokerMessage>> RecoverInFlightMessagesAsync(CancellationToken cancellationToken = default) => Task.FromResult(RecoveredInFlight);

        public Task RemoveMessagesAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default) {
            foreach (var messageId in messageIds) {
                BulkRemovedIds.Add(messageId);
                _messages.Remove(messageId);
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
