using System.Text.Json;
using VibeMQ.Models;
using VibeMQ.Server.Storage;

namespace VibeMQ.Tests.Unit.Storage;

#pragma warning disable CS0618 // Testing obsolete InMemoryMessageStore

public class InMemoryMessageStoreTests {
    private static BrokerMessage CreateMessage(string id, string queueName, DateTime? timestamp = null) {
        return new BrokerMessage {
            Id = id,
            QueueName = queueName,
            Payload = JsonSerializer.SerializeToElement(new { value = 1 }),
            Timestamp = timestamp ?? DateTime.UtcNow
        };
    }

    [Fact]
    public async Task AddAsync_ThenGetAsync_ReturnsSameMessage() {
        var store = new InMemoryMessageStore();
        var msg = CreateMessage("m1", "queue-a");

        var id = await store.AddAsync(msg);
        Assert.Equal("m1", id);

        var got = await store.GetAsync("m1");
        Assert.NotNull(got);
        Assert.Equal("m1", got.Id);
        Assert.Equal("queue-a", got.QueueName);
    }

    [Fact]
    public async Task GetAsync_WhenIdNotFound_ReturnsNull() {
        var store = new InMemoryMessageStore();
        var got = await store.GetAsync("nonexistent");
        Assert.Null(got);
    }

    [Fact]
    public async Task RemoveAsync_WhenExists_ReturnsTrue() {
        var store = new InMemoryMessageStore();
        var msg = CreateMessage("m1", "q");
        await store.AddAsync(msg);

        var removed = await store.RemoveAsync("m1");
        Assert.True(removed);

        var got = await store.GetAsync("m1");
        Assert.Null(got);
    }

    [Fact]
    public async Task RemoveAsync_WhenNotExists_ReturnsFalse() {
        var store = new InMemoryMessageStore();
        var removed = await store.RemoveAsync("nonexistent");
        Assert.False(removed);
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyMessagesForQueue_OrderedByTimestamp() {
        var store = new InMemoryMessageStore();
        var t1 = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2020, 1, 1, 12, 0, 1, DateTimeKind.Utc);
        var t3 = new DateTime(2020, 1, 1, 12, 0, 2, DateTimeKind.Utc);

        await store.AddAsync(CreateMessage("a2", "orders", t2));
        await store.AddAsync(CreateMessage("a1", "orders", t1));
        await store.AddAsync(CreateMessage("other", "other-queue", t1));
        await store.AddAsync(CreateMessage("a3", "orders", t3));

        var pending = await store.GetPendingAsync("orders");

        Assert.Equal(3, pending.Count);
        Assert.Equal("a1", pending[0].Id);
        Assert.Equal("a2", pending[1].Id);
        Assert.Equal("a3", pending[2].Id);
    }

    [Fact]
    public async Task GetPendingAsync_WhenQueueEmpty_ReturnsEmptyList() {
        var store = new InMemoryMessageStore();
        var pending = await store.GetPendingAsync("empty-queue");
        Assert.Empty(pending);
    }

    [Fact]
    public async Task AddAsync_WithSameId_FirstWins_TryAddDoesNotOverwrite() {
        var store = new InMemoryMessageStore();
        var m1 = CreateMessage("same", "q1");
        var m2 = CreateMessage("same", "q2");

        await store.AddAsync(m1);
        await store.AddAsync(m2);

        var got = await store.GetAsync("same");
        Assert.NotNull(got);
        Assert.Equal("q1", got.QueueName);
    }
}
