using System.Collections.Concurrent;
using VibeMQ.Interfaces;
using VibeMQ.Models;

namespace VibeMQ.Server.Storage;

/// <summary>
/// In-memory implementation of <see cref="IMessageStore"/>.
/// Messages are stored in a thread-safe dictionary.
/// </summary>
public sealed class InMemoryMessageStore : IMessageStore {
    private readonly ConcurrentDictionary<string, BrokerMessage> _messages = new();

    /// <inheritdoc />
    public Task<string> AddAsync(BrokerMessage message, CancellationToken cancellationToken = default) {
        _messages.TryAdd(message.Id, message);
        return Task.FromResult(message.Id);
    }

    /// <inheritdoc />
    public Task<BrokerMessage?> GetAsync(string id, CancellationToken cancellationToken = default) {
        _messages.TryGetValue(id, out var message);
        return Task.FromResult(message);
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default) {
        return Task.FromResult(_messages.TryRemove(id, out _));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BrokerMessage>> GetPendingAsync(string queueName, CancellationToken cancellationToken = default) {
        IReadOnlyList<BrokerMessage> pending = _messages.Values
            .Where(m => m.QueueName == queueName)
            .OrderBy(m => m.Timestamp)
            .ToArray();

        return Task.FromResult(pending);
    }
}
