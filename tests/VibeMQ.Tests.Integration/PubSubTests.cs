using System.Collections.Concurrent;
using VibeMQ.Interfaces;

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
    public async Task PublishAndSubscribe_ClassBasedHandler_Delivered() {
        await using var publisher = await _fixture.CreateClientAsync();
        await using var subscriber = await _fixture.CreateClientAsync();

        var queueName = $"class-queue-{Guid.NewGuid():N}";
        var messageName = $"class-msg-{Guid.NewGuid():N}";
        var receivedTask = ClassBasedTestPayloadHandler.Expect(messageName);

        await using var subscription = await subscriber.SubscribeAsync<TestPayload, ClassBasedTestPayloadHandler>(queueName);

        await Task.Delay(100);

        await publisher.PublishAsync(queueName, new TestPayload { Name = messageName, Value = 777 });

        var received = await receivedTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(messageName, received.Name);
        Assert.Equal(777, received.Value);
    }

    [Fact]
    public async Task Subscribe_ClassBasedHandlerWithoutResolvableConstructor_Throws() {
        await using var subscriber = await _fixture.CreateClientAsync();

        var queueName = $"ctor-queue-{Guid.NewGuid():N}";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => subscriber.SubscribeAsync<TestPayload, HandlerWithoutDefaultConstructor>(queueName)
        );

        Assert.Contains("Failed to create instance", ex.Message);
    }

    [Fact]
    public async Task Subscribe_ClassBasedHandler_CancellationTokenCancelledAfterSubscription_StillProcessesMessages() {
        await using var publisher = await _fixture.CreateClientAsync();
        await using var subscriber = await _fixture.CreateClientAsync();

        var queueName = $"class-cancel-queue-{Guid.NewGuid():N}";
        var messageName = $"class-cancel-msg-{Guid.NewGuid():N}";
        var receivedTask = CancelAwareClassBasedHandler.Expect(messageName);
        using var subscribeCts = new CancellationTokenSource();

        await using var subscription = await subscriber.SubscribeAsync<TestPayload, CancelAwareClassBasedHandler>(
            queueName,
            subscribeCts.Token
        );

        subscribeCts.Cancel();
        await Task.Delay(100);

        await publisher.PublishAsync(queueName, new TestPayload { Name = messageName, Value = 551 });
        var received = await receivedTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(messageName, received.Name);
        Assert.Equal(551, received.Value);
    }

    [Fact]
    public async Task Subscribe_ClassBasedHandler_DuplicateQueueOnSameClient_Throws() {
        await using var subscriber = await _fixture.CreateClientAsync();
        var queueName = $"duplicate-class-queue-{Guid.NewGuid():N}";

        await using var first = await subscriber.SubscribeAsync<TestPayload, ClassBasedTestPayloadHandler>(queueName);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => subscriber.SubscribeAsync<TestPayload, ClassBasedTestPayloadHandler>(queueName)
        );

        Assert.Contains("already subscribed", ex.Message, StringComparison.OrdinalIgnoreCase);
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

public sealed class ClassBasedTestPayloadHandler : IMessageHandler<TestPayload> {
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<TestPayload>> PendingByMessageName = new();

    public static Task<TestPayload> Expect(string messageName) {
        var tcs = new TaskCompletionSource<TestPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingByMessageName[messageName] = tcs;
        return tcs.Task;
    }

    public Task HandleAsync(TestPayload message, CancellationToken cancellationToken) {
        if (PendingByMessageName.TryRemove(message.Name, out var pending)) {
            pending.TrySetResult(message);
        }

        return Task.CompletedTask;
    }
}

public sealed class HandlerWithoutDefaultConstructor : IMessageHandler<TestPayload> {
    public HandlerWithoutDefaultConstructor(string requiredDependency) {
        _ = requiredDependency;
    }

    public Task HandleAsync(TestPayload message, CancellationToken cancellationToken) {
        return Task.CompletedTask;
    }
}

public sealed class CancelAwareClassBasedHandler : IMessageHandler<TestPayload> {
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<TestPayload>> PendingByMessageName = new();

    public static Task<TestPayload> Expect(string messageName) {
        var tcs = new TaskCompletionSource<TestPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingByMessageName[messageName] = tcs;
        return tcs.Task;
    }

    public Task HandleAsync(TestPayload message, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (PendingByMessageName.TryRemove(message.Name, out var pending)) {
            pending.TrySetResult(message);
        }
        return Task.CompletedTask;
    }
}
