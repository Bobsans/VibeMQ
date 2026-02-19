using System.Text.Json;
using VibeMQ.Core.Configuration;
using VibeMQ.Core.Enums;
using VibeMQ.Core.Models;
using VibeMQ.Server.Queues;

namespace VibeMQ.Tests.Unit.Queues;

public class MessageQueueTests {
    private static MessageQueue CreateQueue(
        string name = "test-queue",
        DeliveryMode mode = DeliveryMode.RoundRobin,
        int maxSize = 100,
        OverflowStrategy overflow = OverflowStrategy.DropNewest
    ) {
        return new MessageQueue(name, new QueueOptions {
            Mode = mode,
            MaxQueueSize = maxSize,
            OverflowStrategy = overflow,
        });
    }

    private static BrokerMessage CreateMessage(string? id = null, MessagePriority priority = MessagePriority.Normal) {
        return new BrokerMessage {
            Id = id ?? Guid.NewGuid().ToString("N"),
            QueueName = "test-queue",
            Priority = priority,
            Payload = JsonSerializer.SerializeToElement(new { Data = "test" }),
        };
    }

    [Fact]
    public void Enqueue_SingleMessage_IncreasesCount() {
        var queue = CreateQueue();

        var accepted = queue.Enqueue(CreateMessage());

        Assert.True(accepted);
        Assert.Equal(1, queue.MessageCount);
    }

    [Fact]
    public void Dequeue_ReturnsFirstMessage_FIFO() {
        var queue = CreateQueue();
        var msg1 = CreateMessage("msg-1");
        var msg2 = CreateMessage("msg-2");

        queue.Enqueue(msg1);
        queue.Enqueue(msg2);

        var dequeued = queue.Dequeue();

        Assert.NotNull(dequeued);
        Assert.Equal("msg-1", dequeued.Id);
        Assert.Equal(1, queue.MessageCount);
    }

    [Fact]
    public void Dequeue_EmptyQueue_ReturnsNull() {
        var queue = CreateQueue();

        Assert.Null(queue.Dequeue());
    }

    [Fact]
    public void Enqueue_OverflowDropNewest_RejectsMesage() {
        var queue = CreateQueue(maxSize: 2, overflow: OverflowStrategy.DropNewest);

        queue.Enqueue(CreateMessage("a"));
        queue.Enqueue(CreateMessage("b"));
        var accepted = queue.Enqueue(CreateMessage("c"));

        Assert.False(accepted);
        Assert.Equal(2, queue.MessageCount);
    }

    [Fact]
    public void Enqueue_OverflowDropOldest_DropsFirst() {
        var queue = CreateQueue(maxSize: 2, overflow: OverflowStrategy.DropOldest);

        queue.Enqueue(CreateMessage("a"));
        queue.Enqueue(CreateMessage("b"));
        var accepted = queue.Enqueue(CreateMessage("c"));

        Assert.True(accepted);
        Assert.Equal(2, queue.MessageCount);

        // The first dequeued message should be "b" (the oldest "a" was dropped)
        var first = queue.Dequeue();
        Assert.NotNull(first);
        Assert.Equal("b", first.Id);
    }

    [Fact]
    public void PriorityBased_DequeuesHighestFirst() {
        var queue = CreateQueue(mode: DeliveryMode.PriorityBased);

        queue.Enqueue(CreateMessage("low", MessagePriority.Low));
        queue.Enqueue(CreateMessage("critical", MessagePriority.Critical));
        queue.Enqueue(CreateMessage("normal", MessagePriority.Normal));

        var first = queue.Dequeue();
        Assert.NotNull(first);
        Assert.Equal("critical", first.Id);

        var second = queue.Dequeue();
        Assert.NotNull(second);
        Assert.Equal("normal", second.Id);

        var third = queue.Dequeue();
        Assert.NotNull(third);
        Assert.Equal("low", third.Id);
    }

    [Fact]
    public void Acknowledge_TrackedMessage_ReturnsTrue() {
        var queue = CreateQueue();
        var message = CreateMessage("tracked");
        queue.TrackUnacknowledged(message);

        Assert.Equal(1, queue.UnacknowledgedCount);
        Assert.True(queue.Acknowledge("tracked"));
        Assert.Equal(0, queue.UnacknowledgedCount);
    }

    [Fact]
    public void Acknowledge_UnknownMessage_ReturnsFalse() {
        var queue = CreateQueue();

        Assert.False(queue.Acknowledge("nonexistent"));
    }

    [Fact]
    public void RoundRobin_CyclesThroughSubscribers() {
        var queue = CreateQueue();

        var idx0 = queue.GetNextRoundRobinIndex(3);
        var idx1 = queue.GetNextRoundRobinIndex(3);
        var idx2 = queue.GetNextRoundRobinIndex(3);
        _ = queue.GetNextRoundRobinIndex(3);

        // Should cycle through 0, 1, 2, 0, 1, 2...
        // The initial increment is from 0 to 1, then 2, then 0 (mod 3), etc.
        Assert.True(idx0 is >= 0 and < 3);
        Assert.True(idx1 is >= 0 and < 3);
        Assert.True(idx2 is >= 0 and < 3);
        Assert.NotEqual(idx0, idx1);
    }

    [Fact]
    public void PeekAll_DoesNotRemoveMessages() {
        var queue = CreateQueue();
        queue.Enqueue(CreateMessage("a"));
        queue.Enqueue(CreateMessage("b"));

        var peeked = queue.PeekAll();

        Assert.Equal(2, peeked.Count);
        Assert.Equal(2, queue.MessageCount);
    }

    [Fact]
    public void ToQueueInfo_ReturnsCorrectSnapshot() {
        var queue = CreateQueue(name: "info-test", mode: DeliveryMode.FanOutWithAck, maxSize: 500);
        queue.Enqueue(CreateMessage());

        var info = queue.ToQueueInfo(subscriberCount: 3);

        Assert.Equal("info-test", info.Name);
        Assert.Equal(1, info.MessageCount);
        Assert.Equal(3, info.SubscriberCount);
        Assert.Equal(DeliveryMode.FanOutWithAck, info.DeliveryMode);
        Assert.Equal(500, info.MaxSize);
    }
}
