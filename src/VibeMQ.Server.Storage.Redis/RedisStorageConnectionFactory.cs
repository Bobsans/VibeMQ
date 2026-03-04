using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace VibeMQ.Server.Storage.Redis;

/// <summary>
/// Manages Redis ConnectionMultiplexer with lazy initialization and reconnection.
/// </summary>
public sealed class RedisStorageConnectionFactory {
    private readonly RedisStorageOptions _options;
    private readonly ILogger _logger;
    private ConnectionMultiplexer? _multiplexer;
    private readonly object _lock = new();

    public RedisStorageConnectionFactory(RedisStorageOptions options, ILogger logger) {
        _options = options;
        _logger = logger;
    }

    public IConnectionMultiplexer GetConnection() {
        if (_multiplexer is { IsConnected: true }) {
            return _multiplexer;
        }

        lock (_lock) {
            if (_multiplexer is { IsConnected: true }) {
                return _multiplexer;
            }

            var old = _multiplexer;

            var config = ConfigurationOptions.Parse(_options.ConnectionString);
            config.ConnectTimeout = _options.ConnectTimeoutMs;
            config.SyncTimeout = _options.SyncTimeoutMs;
            config.AsyncTimeout = _options.SyncTimeoutMs;
            config.AbortOnConnectFail = false;
            config.ConnectRetry = 3;

            _multiplexer = ConnectionMultiplexer.Connect(config);
            _multiplexer.ConnectionFailed += (_, e) =>
                _logger.LogWarning(e.Exception, "Redis connection failed: {EndPoint}", e.EndPoint);
            _multiplexer.ConnectionRestored += (_, e) =>
                _logger.LogInformation("Redis connection restored: {EndPoint}", e.EndPoint);

            if (old is not null) {
                try { old.Dispose(); } catch { /* best effort */ }
            }

            return _multiplexer;
        }
    }

    public void DisposeConnection() {
        lock (_lock) {
            if (_multiplexer is null) {
                return;
            }

            try {
                _multiplexer.Dispose();
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Error disposing Redis connection.");
            }

            _multiplexer = null;
        }
    }
}
