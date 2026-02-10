using VibeMQ.Server.Metrics;

namespace VibeMQ.Tests.Unit.Metrics;

public class BrokerMetricsTests {
    [Fact]
    public void RecordPublished_IncrementsCounter() {
        var metrics = new BrokerMetrics();

        metrics.RecordPublished();
        metrics.RecordPublished();

        Assert.Equal(2, metrics.TotalMessagesPublished);
    }

    [Fact]
    public void RecordDelivered_TracksCountAndLatency() {
        var metrics = new BrokerMetrics();

        metrics.RecordDelivered(TimeSpan.FromMilliseconds(10));
        metrics.RecordDelivered(TimeSpan.FromMilliseconds(30));

        Assert.Equal(2, metrics.TotalMessagesDelivered);
        Assert.Equal(20, metrics.AverageDeliveryLatencyMs, precision: 1);
    }

    [Fact]
    public void RecordAcknowledged_IncrementsCounter() {
        var metrics = new BrokerMetrics();

        metrics.RecordAcknowledged();

        Assert.Equal(1, metrics.TotalMessagesAcknowledged);
    }

    [Fact]
    public void RecordError_IncrementsCounter() {
        var metrics = new BrokerMetrics();

        metrics.RecordError();
        metrics.RecordError();
        metrics.RecordError();

        Assert.Equal(3, metrics.TotalErrors);
    }

    [Fact]
    public void RecordConnections_TracksAcceptedAndRejected() {
        var metrics = new BrokerMetrics();

        metrics.RecordConnectionAccepted();
        metrics.RecordConnectionAccepted();
        metrics.RecordConnectionRejected();

        Assert.Equal(2, metrics.TotalConnectionsAccepted);
        Assert.Equal(1, metrics.TotalConnectionsRejected);
    }

    [Fact]
    public void UpdateGauges_SetsValues() {
        var metrics = new BrokerMetrics();

        metrics.UpdateGauges(activeConnections: 5, activeQueues: 3, inFlightMessages: 10);

        Assert.Equal(5, metrics.ActiveConnections);
        Assert.Equal(3, metrics.ActiveQueues);
        Assert.Equal(10, metrics.InFlightMessages);
    }

    [Fact]
    public void AverageLatency_NoDeliveries_ReturnsZero() {
        var metrics = new BrokerMetrics();

        Assert.Equal(0, metrics.AverageDeliveryLatencyMs);
    }

    [Fact]
    public void MemoryUsageBytes_ReturnsPositive() {
        var metrics = new BrokerMetrics();

        Assert.True(metrics.MemoryUsageBytes > 0);
    }

    [Fact]
    public void GetSnapshot_ReturnsCurrentState() {
        var metrics = new BrokerMetrics();

        metrics.RecordPublished();
        metrics.RecordDelivered(TimeSpan.FromMilliseconds(5));
        metrics.RecordAcknowledged();
        metrics.RecordRetry();
        metrics.RecordDeadLettered();
        metrics.UpdateGauges(10, 2, 1);

        var snapshot = metrics.GetSnapshot();

        Assert.Equal(1, snapshot.TotalMessagesPublished);
        Assert.Equal(1, snapshot.TotalMessagesDelivered);
        Assert.Equal(1, snapshot.TotalMessagesAcknowledged);
        Assert.Equal(1, snapshot.TotalRetries);
        Assert.Equal(1, snapshot.TotalDeadLettered);
        Assert.Equal(10, snapshot.ActiveConnections);
        Assert.Equal(2, snapshot.ActiveQueues);
        Assert.Equal(1, snapshot.InFlightMessages);
        Assert.True(snapshot.Uptime.TotalMilliseconds >= 0);
    }

    [Fact]
    public void ThreadSafety_ConcurrentIncrements() {
        var metrics = new BrokerMetrics();
        const int iterations = 10_000;

        Parallel.For(0, iterations, _ => {
            metrics.RecordPublished();
            metrics.RecordDelivered(TimeSpan.FromMilliseconds(1));
            metrics.RecordError();
        });

        Assert.Equal(iterations, metrics.TotalMessagesPublished);
        Assert.Equal(iterations, metrics.TotalMessagesDelivered);
        Assert.Equal(iterations, metrics.TotalErrors);
    }
}
