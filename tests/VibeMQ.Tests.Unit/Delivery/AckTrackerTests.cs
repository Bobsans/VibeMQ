using System.Text.Json;
using VibeMQ.Core.Models;
using VibeMQ.Server.Delivery;

namespace VibeMQ.Tests.Unit.Delivery;

public class AckTrackerTests {
    private static BrokerMessage CreateMessage(string id = "msg-1") {
        return new BrokerMessage {
            Id = id,
            QueueName = "test-queue",
            Payload = JsonSerializer.SerializeToElement("test"),
            DeliveryAttempts = 3,
        };
    }

    [Fact]
    public void Track_IncrementsPendingCount() {
        var tracker = new AckTracker(ackTimeout: TimeSpan.FromSeconds(30));
        tracker.Track(CreateMessage(), "client-1");

        Assert.Equal(1, tracker.PendingCount);
    }

    [Fact]
    public void Acknowledge_RemovesFromPending() {
        var tracker = new AckTracker(ackTimeout: TimeSpan.FromSeconds(30));
        tracker.Track(CreateMessage("msg-1"), "client-1");

        var result = tracker.Acknowledge("msg-1");

        Assert.True(result);
        Assert.Equal(0, tracker.PendingCount);
    }

    [Fact]
    public void Acknowledge_UnknownMessage_ReturnsFalse() {
        var tracker = new AckTracker(ackTimeout: TimeSpan.FromSeconds(30));

        Assert.False(tracker.Acknowledge("nonexistent"));
    }

    [Fact]
    public void IsTracked_TrackedMessage_ReturnsTrue() {
        var tracker = new AckTracker(ackTimeout: TimeSpan.FromSeconds(30));
        tracker.Track(CreateMessage("msg-1"), "client-1");

        Assert.True(tracker.IsTracked("msg-1"));
    }

    [Fact]
    public void IsTracked_UnknownMessage_ReturnsFalse() {
        var tracker = new AckTracker(ackTimeout: TimeSpan.FromSeconds(30));

        Assert.False(tracker.IsTracked("nonexistent"));
    }

    [Fact]
    public void Track_DuplicateId_DoesNotIncrease() {
        var tracker = new AckTracker(ackTimeout: TimeSpan.FromSeconds(30));

        tracker.Track(CreateMessage("dup"), "client-1");
        tracker.Track(CreateMessage("dup"), "client-2");

        Assert.Equal(1, tracker.PendingCount);
    }

    [Fact]
    public async Task Dispose_StopsTracking() {
        var tracker = new AckTracker(ackTimeout: TimeSpan.FromSeconds(30));
        tracker.Start();
        tracker.Track(CreateMessage(), "client-1");

        await tracker.DisposeAsync();

        // Should not throw after disposal
        Assert.Equal(1, tracker.PendingCount);
    }
}
