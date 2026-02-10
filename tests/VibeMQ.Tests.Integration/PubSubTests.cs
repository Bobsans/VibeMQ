namespace VibeMQ.Tests.Integration;

public class PubSubTests : IAsyncLifetime {
    private readonly TestBrokerFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task PublishAndSubscribe_SingleMessage_Delivered() {
        await using var publisher = await _fixture.CreateClientAsync();
        await using var subscriber = await _fixture.CreateClientAsync();

        var received = new TaskCompletionSource<TestPayload>();

        await subscriber.SubscribeAsync<TestPayload>("test-queue", msg => {
            received.TrySetResult(msg);
            return Task.CompletedTask;
        });

        // Allow subscription to register
        await Task.Delay(100);

        await publisher.PublishAsync("test-queue", new TestPayload { Name = "hello", Value = 42 });

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("hello", result.Name);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task PublishAndSubscribe_MultipleMessages_AllDelivered() {
        await using var publisher = await _fixture.CreateClientAsync();
        await using var subscriber = await _fixture.CreateClientAsync();

        var count = 0;
        var allReceived = new TaskCompletionSource();

        await subscriber.SubscribeAsync<TestPayload>("multi-queue", _ => {
            if (Interlocked.Increment(ref count) >= 10) {
                allReceived.TrySetResult();
            }

            return Task.CompletedTask;
        });

        await Task.Delay(100);

        for (var i = 0; i < 10; i++) {
            await publisher.PublishAsync("multi-queue", new TestPayload { Name = $"msg-{i}", Value = i });
        }

        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(10, count);
    }

    [Fact]
    public async Task Subscribe_ThenUnsubscribe_StopsReceiving() {
        await using var publisher = await _fixture.CreateClientAsync();
        await using var subscriber = await _fixture.CreateClientAsync();

        var received = 0;

        var subscription = await subscriber.SubscribeAsync<TestPayload>("unsub-queue", _ => {
            Interlocked.Increment(ref received);
            return Task.CompletedTask;
        });

        await Task.Delay(100);

        await publisher.PublishAsync("unsub-queue", new TestPayload { Name = "before" });
        await Task.Delay(300);

        var countBeforeUnsub = received;
        Assert.True(countBeforeUnsub > 0);

        // Unsubscribe
        await subscription.DisposeAsync();
        await Task.Delay(100);

        await publisher.PublishAsync("unsub-queue", new TestPayload { Name = "after" });
        await Task.Delay(300);

        // Should not receive more messages
        Assert.Equal(countBeforeUnsub, received);
    }

    [Fact]
    public async Task Connection_WithAuthentication_Succeeds() {
        await using var client = await _fixture.CreateClientAsync(authenticate: true);

        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Disconnect_GracefullyClosesConnection() {
        var client = await _fixture.CreateClientAsync();

        Assert.True(client.IsConnected);

        await client.DisconnectAsync();

        Assert.False(client.IsConnected);
    }
}

public class TestPayload {
    public string Name { get; set; } = "";
    public int Value { get; set; }
}
