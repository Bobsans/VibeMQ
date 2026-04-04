using System.Collections.Concurrent;
using VibeMQ.Client;
using VibeMQ.Configuration;
using VibeMQ.Interfaces;
using VibeMQ.Models;

namespace VibeMQ.Tests.Unit.Client;

/// <summary>
/// Fake IVibeMQClient that records SubscribeAsync&lt;TMessage, THandler&gt; calls for unit testing MessageHandlerHostedService.
/// </summary>
sealed class FakeRecordingVibeMQClient : IVibeMQClient {
    public bool IsConnected => false;

    private readonly ConcurrentBag<(string QueueName, Type MessageType, Type HandlerType)> _subscriptions = [];

    public IReadOnlyList<(string QueueName, Type MessageType, Type HandlerType)> RecordedSubscriptions =>
        _subscriptions.ToList();

    public Task PublishAsync<T>(string queueName, T payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishAsync<T>(string queueName, T payload, Dictionary<string, string>? headers, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IAsyncDisposable> SubscribeAsync<T>(string queueName, Func<T, Task> handler, CancellationToken cancellationToken = default) {
        _subscriptions.Add((queueName, typeof(T), typeof(Func<T, Task>)));
        return Task.FromResult<IAsyncDisposable>(new NoOpSubscription());
    }

    public Task<IAsyncDisposable> SubscribeAsync<TMessage, THandler>(string queueName, CancellationToken cancellationToken = default)
        where THandler : IMessageHandler<TMessage> {
        _subscriptions.Add((queueName, typeof(TMessage), typeof(THandler)));
        return Task.FromResult<IAsyncDisposable>(new NoOpSubscription());
    }

    public Task CreateQueueAsync(string queueName, QueueOptions? options = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<QueueInfo?> GetQueueInfoAsync(string queueName, CancellationToken cancellationToken = default) => Task.FromResult<QueueInfo?>(null);
    public Task<IReadOnlyList<string>> ListQueuesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);

    private sealed class NoOpSubscription : IAsyncDisposable {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
