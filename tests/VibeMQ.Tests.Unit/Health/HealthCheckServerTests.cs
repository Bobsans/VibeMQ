using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using VibeMQ.Configuration;
using VibeMQ.Health;
using VibeMQ.Metrics;

namespace VibeMQ.Tests.Unit.Health;

/// <summary>
/// Tests for HealthCheckServer: GET /health and /metrics via ProcessRequest (no HttpListener, no admin rights).
/// </summary>
public sealed class HealthCheckServerTests {
    private static readonly JsonSerializerOptions _jsonOptions = new() {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public async Task ProcessRequest_Health_WhenHealthy_Returns200AndJson() {
        var status = new HealthStatus {
            IsHealthy = true,
            Status = "ok",
            ActiveConnections = 2,
            QueueCount = 1,
            InFlightMessages = 0,
            TotalMessagesPublished = 100,
            TotalMessagesDelivered = 98,
            Timestamp = DateTime.UtcNow
        };

        var (statusCode, contentType, writeBody) = HealthCheckServer.ProcessRequest("/health", status, null);

        Assert.Equal(200, statusCode);
        Assert.Equal("application/json", contentType);
        Assert.NotNull(writeBody);

        using var stream = new MemoryStream();
        await writeBody(stream);
        stream.Position = 0;
        var json = await new StreamReader(stream).ReadToEndAsync();
        var dto = JsonSerializer.Deserialize<HealthStatusDto>(json, _jsonOptions);
        Assert.NotNull(dto);
        Assert.True(dto.IsHealthy);
        Assert.Equal("ok", dto.Status);
        Assert.Equal(2, dto.ActiveConnections);
        Assert.Equal(1, dto.QueueCount);
    }

    [Fact]
    public async Task ProcessRequest_Health_WhenUnhealthy_Returns503() {
        var status = new HealthStatus {
            IsHealthy = false,
            Status = "degraded",
            ActiveConnections = 0,
            QueueCount = 0,
            InFlightMessages = 0,
            TotalMessagesPublished = 0,
            TotalMessagesDelivered = 0,
            Timestamp = DateTime.UtcNow
        };

        var (statusCode, contentType, writeBody) = HealthCheckServer.ProcessRequest("/health", status, null);

        Assert.Equal(503, statusCode);
        Assert.Equal("application/json", contentType);
        Assert.NotNull(writeBody);

        using var stream = new MemoryStream();
        await writeBody(stream);
        stream.Position = 0;
        var json = await new StreamReader(stream).ReadToEndAsync();
        var dto = JsonSerializer.Deserialize<HealthStatusDto>(json, _jsonOptions);
        Assert.NotNull(dto);
        Assert.False(dto.IsHealthy);
        Assert.Equal("degraded", dto.Status);
    }

    [Fact]
    public async Task ProcessRequest_Metrics_WhenProviderSet_Returns200AndJson() {
        var healthStatus = new HealthStatus {
            IsHealthy = true,
            Status = "ok",
            ActiveConnections = 0,
            QueueCount = 0,
            InFlightMessages = 0,
            TotalMessagesPublished = 0,
            TotalMessagesDelivered = 0,
            Timestamp = DateTime.UtcNow
        };
        var metrics = new MetricsSnapshot {
            TotalMessagesPublished = 10,
            TotalMessagesDelivered = 8,
            ActiveConnections = 1,
            ActiveQueues = 2,
            InFlightMessages = 1,
            MemoryUsageBytes = 1024 * 1024,
            Timestamp = DateTime.UtcNow,
            Uptime = TimeSpan.FromMinutes(5)
        };

        var (statusCode, contentType, writeBody) = HealthCheckServer.ProcessRequest("/metrics", healthStatus, metrics);

        Assert.Equal(200, statusCode);
        Assert.Equal("application/json", contentType);
        Assert.NotNull(writeBody);

        using var stream = new MemoryStream();
        await writeBody(stream);
        stream.Position = 0;
        var json = await new StreamReader(stream).ReadToEndAsync();
        var dto = JsonSerializer.Deserialize<MetricsSnapshotDto>(json, _jsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(10, dto.TotalMessagesPublished);
        Assert.Equal(8, dto.TotalMessagesDelivered);
        Assert.Equal(1, dto.ActiveConnections);
        Assert.Equal(2, dto.ActiveQueues);
    }

    [Fact]
    public void ProcessRequest_Metrics_WhenProviderNull_Returns404() {
        var healthStatus = new HealthStatus {
            IsHealthy = true,
            Status = "ok",
            ActiveConnections = 0,
            QueueCount = 0,
            InFlightMessages = 0,
            TotalMessagesPublished = 0,
            TotalMessagesDelivered = 0,
            Timestamp = DateTime.UtcNow
        };

        var (statusCode, contentType, writeBody) = HealthCheckServer.ProcessRequest("/metrics", healthStatus, null);

        Assert.Equal(404, statusCode);
        Assert.Null(contentType);
        Assert.Null(writeBody);
    }

    [Fact]
    public void ProcessRequest_UnknownPath_Returns404() {
        var healthStatus = new HealthStatus {
            IsHealthy = true,
            Status = "ok",
            ActiveConnections = 0,
            QueueCount = 0,
            InFlightMessages = 0,
            TotalMessagesPublished = 0,
            TotalMessagesDelivered = 0,
            Timestamp = DateTime.UtcNow
        };

        var (statusCode, contentType, writeBody) = HealthCheckServer.ProcessRequest("/unknown", healthStatus, null);

        Assert.Equal(404, statusCode);
        Assert.Null(contentType);
        Assert.Null(writeBody);
    }

    [Fact]
    public void Start_WhenDisabled_DoesNotThrow() {
        var options = new HealthCheckOptions { Enabled = false, Port = 2926 };

        var server = new HealthCheckServer(options, NullLogger<HealthCheckServer>.Instance, () => new HealthStatus {
            IsHealthy = true,
            Status = "ok",
            ActiveConnections = 0,
            QueueCount = 0,
            InFlightMessages = 0,
            TotalMessagesPublished = 0,
            TotalMessagesDelivered = 0,
            Timestamp = DateTime.UtcNow
        }, metricsProvider: null);
        server.Start();
    }

    private sealed class HealthStatusDto {
        public bool IsHealthy { get; set; }
        public string? Status { get; set; }
        public int ActiveConnections { get; set; }
        public int QueueCount { get; set; }
    }

    private sealed class MetricsSnapshotDto {
        public long TotalMessagesPublished { get; set; }
        public long TotalMessagesDelivered { get; set; }
        public int ActiveConnections { get; set; }
        public int ActiveQueues { get; set; }
    }
}
