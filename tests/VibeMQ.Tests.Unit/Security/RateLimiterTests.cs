using Microsoft.Extensions.Logging.Abstractions;
using VibeMQ.Configuration;
using VibeMQ.Server.Security;

namespace VibeMQ.Tests.Unit.Security;

public class RateLimiterTests {
    [Fact]
    public void ConnectionAllowed_WithinLimit_ReturnsTrue() {
        var limiter = new RateLimiter(
            new RateLimitOptions { MaxConnectionsPerIpPerWindow = 5, ConnectionWindow = TimeSpan.FromSeconds(60) },
            NullLogger<RateLimiter>.Instance
        );

        for (var i = 0; i < 5; i++) {
            Assert.True(limiter.IsConnectionAllowed("192.168.1.1"));
        }
    }

    [Fact]
    public void ConnectionAllowed_ExceedsLimit_ReturnsFalse() {
        var limiter = new RateLimiter(
            new RateLimitOptions { MaxConnectionsPerIpPerWindow = 3, ConnectionWindow = TimeSpan.FromSeconds(60) },
            NullLogger<RateLimiter>.Instance
        );

        for (var i = 0; i < 3; i++) {
            Assert.True(limiter.IsConnectionAllowed("192.168.1.1"));
        }

        Assert.False(limiter.IsConnectionAllowed("192.168.1.1"));
    }

    [Fact]
    public void ConnectionAllowed_DifferentIps_IndependentLimits() {
        var limiter = new RateLimiter(
            new RateLimitOptions { MaxConnectionsPerIpPerWindow = 2, ConnectionWindow = TimeSpan.FromSeconds(60) },
            NullLogger<RateLimiter>.Instance
        );

        Assert.True(limiter.IsConnectionAllowed("10.0.0.1"));
        Assert.True(limiter.IsConnectionAllowed("10.0.0.1"));
        Assert.False(limiter.IsConnectionAllowed("10.0.0.1"));

        // Different IP should still be allowed
        Assert.True(limiter.IsConnectionAllowed("10.0.0.2"));
    }

    [Fact]
    public void MessageAllowed_WithinLimit_ReturnsTrue() {
        var limiter = new RateLimiter(
            new RateLimitOptions { MaxMessagesPerClientPerSecond = 100 },
            NullLogger<RateLimiter>.Instance
        );

        for (var i = 0; i < 100; i++) {
            Assert.True(limiter.IsMessageAllowed("client-1"));
        }
    }

    [Fact]
    public void MessageAllowed_ExceedsLimit_ReturnsFalse() {
        var limiter = new RateLimiter(
            new RateLimitOptions { MaxMessagesPerClientPerSecond = 5 },
            NullLogger<RateLimiter>.Instance
        );

        for (var i = 0; i < 5; i++) {
            Assert.True(limiter.IsMessageAllowed("client-1"));
        }

        Assert.False(limiter.IsMessageAllowed("client-1"));
    }

    [Fact]
    public void Disabled_AlwaysAllows() {
        var limiter = new RateLimiter(
            new RateLimitOptions { Enabled = false },
            NullLogger<RateLimiter>.Instance
        );

        for (var i = 0; i < 1000; i++) {
            Assert.True(limiter.IsConnectionAllowed("any-ip"));
            Assert.True(limiter.IsMessageAllowed("any-client"));
        }
    }

    [Fact]
    public void RemoveClient_ClearsMessageTracking() {
        var limiter = new RateLimiter(
            new RateLimitOptions { MaxMessagesPerClientPerSecond = 2 },
            NullLogger<RateLimiter>.Instance
        );

        Assert.True(limiter.IsMessageAllowed("client-1"));
        Assert.True(limiter.IsMessageAllowed("client-1"));
        Assert.False(limiter.IsMessageAllowed("client-1"));

        limiter.RemoveClient("client-1");

        // After removal, new window starts
        Assert.True(limiter.IsMessageAllowed("client-1"));
    }
}
