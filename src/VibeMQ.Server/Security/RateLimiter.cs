using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using VibeMQ.Core.Configuration;

namespace VibeMQ.Server.Security;

/// <summary>
/// Sliding window rate limiter for connections (per IP) and messages (per client).
/// </summary>
public sealed partial class RateLimiter {
    private readonly RateLimitOptions _options;
    private readonly ILogger<RateLimiter> _logger;
    private readonly ConcurrentDictionary<string, SlidingWindow> _connectionWindows = new();
    private readonly ConcurrentDictionary<string, SlidingWindow> _messageWindows = new();

    public RateLimiter(RateLimitOptions options, ILogger<RateLimiter> logger) {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a new connection from the given IP is allowed.
    /// </summary>
    public bool IsConnectionAllowed(string ipAddress) {
        if (!_options.Enabled) {
            return true;
        }

        var window = _connectionWindows.GetOrAdd(ipAddress, _ => new SlidingWindow(_options.ConnectionWindow));
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

        var window = _messageWindows.GetOrAdd(clientId, _ => new SlidingWindow(TimeSpan.FromSeconds(1)));
        var allowed = window.TryRecord(_options.MaxMessagesPerClientPerSecond);

        if (!allowed) {
            LogMessageRateLimited(clientId);
        }

        return allowed;
    }

    /// <summary>
    /// Removes tracking data for a disconnected client.
    /// </summary>
    public void RemoveClient(string clientId) {
        _messageWindows.TryRemove(clientId, out _);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection rate limit exceeded for IP {ipAddress}.")]
    private partial void LogConnectionRateLimited(string ipAddress);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message rate limit exceeded for client {clientId}.")]
    private partial void LogMessageRateLimited(string clientId);

    /// <summary>
    /// Thread-safe sliding window counter.
    /// </summary>
    private sealed class SlidingWindow {
        private readonly TimeSpan _windowSize;
        private readonly ConcurrentQueue<DateTime> _timestamps = new();
        private readonly object _lock = new();

        public SlidingWindow(TimeSpan windowSize) {
            _windowSize = windowSize;
        }

        /// <summary>
        /// Tries to record a new event. Returns false if the limit has been reached.
        /// </summary>
        public bool TryRecord(int maxEvents) {
            var now = DateTime.UtcNow;
            var cutoff = now - _windowSize;

            // Evict expired entries
            while (_timestamps.TryPeek(out var oldest) && oldest < cutoff) {
                _timestamps.TryDequeue(out _);
            }

            lock (_lock) {
                if (_timestamps.Count >= maxEvents) {
                    return false;
                }

                _timestamps.Enqueue(now);
                return true;
            }
        }
    }
}
