using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace VibeMQ.Server.Storage.Redis;

/// <summary>
/// Manages Redis ConnectionMultiplexer with lazy initialization and reconnection.
/// </summary>
public sealed class RedisStorageConnectionFactory(RedisStorageOptions options, ILogger logger) : IDisposable {
    private ConnectionMultiplexer? _multiplexer;
#if NET10_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    public IConnectionMultiplexer GetConnection() {
        lock (_lock) {
            if (_multiplexer is { IsConnected: true }) {
                return _multiplexer;
            }

            var old = _multiplexer;

            var config = ConfigurationOptions.Parse(options.ConnectionString);
            config.ConnectTimeout = options.ConnectTimeoutMs;
            config.SyncTimeout = options.SyncTimeoutMs;
            config.AsyncTimeout = options.SyncTimeoutMs;
            config.AbortOnConnectFail = false;
            config.ConnectRetry = 3;

            _multiplexer = ConnectionMultiplexer.Connect(config);
            _multiplexer.ConnectionFailed += (_, e) =>
                logger.LogWarning(e.Exception, "Redis connection failed: {EndPoint}", e.EndPoint);
            _multiplexer.ConnectionRestored += (_, e) =>
                logger.LogInformation("Redis connection restored: {EndPoint}", e.EndPoint);

            if (old is not null) {
                try {
                    old.Dispose();
                } catch {
                    /* best effort */
                }
            }

            return _multiplexer;
        }
    }

    public void Dispose() => DisposeConnection();

    public void DisposeConnection() {
        lock (_lock) {
            if (_multiplexer is null) {
                return;
            }

            try {
                _multiplexer.Dispose();
            } catch (Exception ex) {
                logger.LogWarning(ex, "Error disposing Redis connection.");
            }

            _multiplexer = null;
        }
    }
}
