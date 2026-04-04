using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using VibeMQ.Configuration;

namespace VibeMQ.Server.Security;

/// <summary>
/// Sliding window rate limiter for connections (per IP) and messages (per client).
/// </summary>
public sealed partial class RateLimiter : IDisposable {
    private readonly RateLimitOptions _options;
    private readonly ILogger<RateLimiter> _logger;
    private readonly ConcurrentDictionary<string, Lazy<SlidingWindow>> _connectionWindows = new();
    private readonly ConcurrentDictionary<string, Lazy<SlidingWindow>> _messageWindows = new();
    private readonly Timer _cleanupTimer;

    public RateLimiter(RateLimitOptions options, ILogger<RateLimiter> logger) {
        _options = options;
        _logger = logger;
        _cleanupTimer = new Timer(_ => PruneStaleWindows(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Checks if a new connection from the given IP is allowed.
    /// </summary>
    public bool IsConnectionAllowed(string ipAddress) {
        if (!_options.Enabled) {
            return true;
        }

        var window = _connectionWindows.GetOrAdd(ipAddress, _ => new Lazy<SlidingWindow>(() => new SlidingWindow(_options.ConnectionWindow))).Value;
        var allowed = window.TryRecord(_options.MaxConnectionsPerIpPerWindow);

        if (!allowed) {
            LogConnectionRateLimited(ipAddress);
        }

        return allowed;
    }

    /// <summary>
    /// Checks if a message from the given client is allowed.
    /// </summary>
    public bool IsMessageAllowed(string clientId) {
        if (!_options.Enabled) {
            return true;
        }

        var window = _messageWindows.GetOrAdd(clientId, _ => new Lazy<SlidingWindow>(() => new SlidingWindow(TimeSpan.FromSeconds(1)))).Value;
        var allowed = window.TryRecord(_options.MaxMessagesPerClientPerSecond);

        if (!allowed) {
            LogMessageRateLimited(clientId);
        }

        return allowed;
    }

    /// <summary>
    /// Removes tracking data for a disconnected client and its IP connection window.
    /// </summary>
    public void RemoveClient(string clientId, string? ipAddress = null) {
        _messageWindows.TryRemove(clientId, out _);

        if (ipAddress is not null) {
            _connectionWindows.TryRemove(ipAddress, out _);
        }
    }

    private void PruneStaleWindows() {
        // Remove connection windows for IPs with no recent activity.
        // SlidingWindow.IsExpired checks whether all timestamps fell outside the window.
        foreach (var (key, lazy) in _connectionWindows) {
            if (lazy is { IsValueCreated: true, Value.IsExpired: true }) {
                _connectionWindows.TryRemove(key, out _);
            }
        }

        foreach (var (key, lazy) in _messageWindows) {
            if (lazy is { IsValueCreated: true, Value.IsExpired: true }) {
                _messageWindows.TryRemove(key, out _);
            }
        }
    }

    public void Dispose() {
        _cleanupTimer.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection rate limit exceeded for IP {ipAddress}.")]
    private partial void LogConnectionRateLimited(string ipAddress);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message rate limit exceeded for client {clientId}.")]
    private partial void LogMessageRateLimited(string clientId);

    /// <summary>
    /// Thread-safe sliding window counter.
    /// </summary>
    private sealed class SlidingWindow(TimeSpan windowSize) {
        private readonly ConcurrentQueue<DateTime> _timestamps = new();
#if NET10_0_OR_GREATER
        private readonly Lock _lock = new();
#else
        private readonly object _lock = new();
#endif

        /// <summary>
        /// Returns true when the window contains no recent timestamps (safe to evict).
        /// </summary>
        public bool IsExpired {
            get {
                var cutoff = DateTime.UtcNow - windowSize;
                lock (_lock) {
                    while (_timestamps.TryPeek(out var oldest) && oldest < cutoff) {
                        _timestamps.TryDequeue(out _);
                    }
                    return _timestamps.IsEmpty;
                }
            }
        }

        /// <summary>
        /// Tries to record a new event. Returns false if the limit has been reached.
        /// </summary>
        public bool TryRecord(int maxEvents) {
            var now = DateTime.UtcNow;
            var cutoff = now - windowSize;

            lock (_lock) {
                // Evict expired entries
                while (_timestamps.TryPeek(out var oldest) && oldest < cutoff) {
                    _timestamps.TryDequeue(out _);
                }

                if (_timestamps.Count >= maxEvents) {
                    return false;
                }

                _timestamps.Enqueue(now);
                return true;
            }
        }
    }
}
