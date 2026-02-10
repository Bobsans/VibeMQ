using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using VibeMQ.Core.Enums;
using VibeMQ.Core.Models;
using VibeMQ.Server.Delivery;

namespace VibeMQ.Tests.Unit.Delivery;

public class DeadLetterQueueTests {
    private static BrokerMessage CreateMessage(string id = "msg-1") {
        return new BrokerMessage {
            Id = id,
            QueueName = "test-queue",
            Payload = JsonSerializer.SerializeToElement("test"),
        };
    }

    [Fact]
    public async Task HandleFailed_AddsToQueue() {
        var dlq = new DeadLetterQueue(NullLogger<DeadLetterQueue>.Instance);

        await dlq.HandleFailedMessageAsync(CreateMessage(), FailureReason.MaxRetriesExceeded);

        Assert.Equal(1, dlq.Count);
    }

    [Fact]
    public async Task GetMessages_ReturnsRequestedCount() {
        var dlq = new DeadLetterQueue(NullLogger<DeadLetterQueue>.Instance);

        await dlq.HandleFailedMessageAsync(CreateMessage("a"), FailureReason.MaxRetriesExceeded);
        await dlq.HandleFailedMessageAsync(CreateMessage("b"), FailureReason.MaxRetriesExceeded);
        await dlq.HandleFailedMessageAsync(CreateMessage("c"), FailureReason.MaxRetriesExceeded);

        var messages = await dlq.GetMessagesAsync(2);

        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public async Task Dequeue_ReturnsAndRemoves() {
        var dlq = new DeadLetterQueue(NullLogger<DeadLetterQueue>.Instance);

        await dlq.HandleFailedMessageAsync(CreateMessage("a"), FailureReason.MaxRetriesExceeded);

        var dequeued = await dlq.DequeueAsync();

        Assert.NotNull(dequeued);
        Assert.Equal("a", dequeued.OriginalMessage.Id);
        Assert.Equal(FailureReason.MaxRetriesExceeded, dequeued.Reason);
        Assert.Equal(0, dlq.Count);
    }

    [Fact]
    public async Task Dequeue_Empty_ReturnsNull() {
        var dlq = new DeadLetterQueue(NullLogger<DeadLetterQueue>.Instance);

        var result = await dlq.DequeueAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleFailed_SetsFailedAtTimestamp() {
        var dlq = new DeadLetterQueue(NullLogger<DeadLetterQueue>.Instance);
        var before = DateTime.UtcNow;

        await dlq.HandleFailedMessageAsync(CreateMessage(), FailureReason.MaxRetriesExceeded);

        var dequeued = await dlq.DequeueAsync();
        Assert.NotNull(dequeued);
        Assert.True(dequeued.FailedAt >= before);
        Assert.True(dequeued.FailedAt <= DateTime.UtcNow);
    }
}
