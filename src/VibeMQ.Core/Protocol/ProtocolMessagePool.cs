using System.Collections.Concurrent;

namespace VibeMQ.Protocol;

/// <summary>
/// Lock-free object pool for <see cref="ProtocolMessage"/> instances.
/// Reduces GC pressure on hot paths (publish/deliver).
/// </summary>
public static class ProtocolMessagePool {
    private static readonly ConcurrentBag<ProtocolMessage> _pool = new();
    private const int MAX_POOL_SIZE = 256;

    /// <summary>
    /// Rents a <see cref="ProtocolMessage"/> from the pool, or creates a new one if the pool is empty.
    /// </summary>
    public static ProtocolMessage Rent(CommandType type) {
        if (_pool.TryTake(out var message)) {
            message.Id = Guid.NewGuid().ToString("N");
            message.Type = type;
            message.Version = 1;
            return message;
        }

        return new ProtocolMessage { Type = type };
    }

    /// <summary>
    /// Returns a <see cref="ProtocolMessage"/> to the pool for reuse.
    /// Clears all fields before returning.
    /// </summary>
    public static void Return(ProtocolMessage message) {
        if (_pool.Count >= MAX_POOL_SIZE) {
            return; // Don't grow the pool indefinitely
        }

        // Clear all fields
        message.Queue = null;
        message.Payload = null;
        message.Headers = null;
        message.ErrorCode = null;
        message.ErrorMessage = null;

        _pool.Add(message);
    }
}
