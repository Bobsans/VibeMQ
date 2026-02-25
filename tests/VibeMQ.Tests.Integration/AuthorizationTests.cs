using VibeMQ.Enums;

namespace VibeMQ.Tests.Integration;

/// <summary>
/// Integration tests for the username/password authorization system.
/// Each test method gets its own fixture to avoid cross-test state leakage.
/// </summary>
public class AuthorizationTests : IAsyncLifetime {
    private readonly AuthBrokerFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    // ─── Authentication ────────────────────────────────────────────────────

    [Fact]
    public async Task Connect_WithSuperuserCredentials_Succeeds() {
        await using var client = await _fixture.ConnectAsSuperuserAsync();

        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Connect_WithWrongPassword_Throws() {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _fixture.ConnectAsync(AuthBrokerFixture.SuperuserUsername, "wrong-password")
        );
    }

    [Fact]
    public async Task Connect_WithUnknownUser_Throws() {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _fixture.ConnectAsync("no-such-user", "any-password")
        );
    }

    [Fact]
    public async Task Connect_RegularUser_WithCorrectPassword_Succeeds() {
        await _fixture.CreateUserAsync("user-connect", "p@ssw0rd");

        await using var client = await _fixture.ConnectAsync("user-connect", "p@ssw0rd");

        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Connect_RegularUser_WithWrongPassword_Throws() {
        await _fixture.CreateUserAsync("user-wrongpw", "correct-pass");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _fixture.ConnectAsync("user-wrongpw", "wrong-pass")
        );
    }

    // ─── Superuser bypass ─────────────────────────────────────────────────

    [Fact]
    public async Task Publish_Superuser_IsAlwaysAuthorized() {
        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.CreateQueueAsync("su-pub-queue");

        // Should not throw
        await su.PublishAsync("su-pub-queue", new { Value = 42 });
    }

    [Fact]
    public async Task Subscribe_Superuser_IsAlwaysAuthorized() {
        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.CreateQueueAsync("su-sub-queue");

        await using var sub = await su.SubscribeAsync<object>("su-sub-queue", _ => Task.CompletedTask);

        Assert.NotNull(sub);
    }

    // ─── Publish authorization ─────────────────────────────────────────────

    [Fact]
    public async Task Publish_RegularUser_WithPublishPermission_Succeeds() {
        await _fixture.CreateUserAsync("pub-allowed", "pass");
        await _fixture.GrantPermissionAsync("pub-allowed", "pub-allowed.*",
            [QueueOperation.Publish, QueueOperation.CreateQueue]);

        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.CreateQueueAsync("pub-allowed.orders");

        await using var client = await _fixture.ConnectAsync("pub-allowed", "pass");

        await client.PublishAsync("pub-allowed.orders", new { Amount = 100 });
    }

    [Fact]
    public async Task Publish_RegularUser_WithoutPermission_Throws() {
        await _fixture.CreateUserAsync("pub-denied", "pass");
        // No permissions granted

        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.CreateQueueAsync("restricted-pub-queue");

        await using var client = await _fixture.ConnectAsync("pub-denied", "pass");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.PublishAsync("restricted-pub-queue", new { Data = "x" })
        );
    }

    [Fact]
    public async Task Publish_UserWithSubscribeOnly_CannotPublish() {
        await _fixture.CreateUserAsync("sub-only", "pass");
        await _fixture.GrantPermissionAsync("sub-only", "sub-only.*",
            [QueueOperation.Subscribe]);  // subscribe but NOT publish

        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.CreateQueueAsync("sub-only.events");

        await using var client = await _fixture.ConnectAsync("sub-only", "pass");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.PublishAsync("sub-only.events", new { Data = "x" })
        );
    }

    // ─── Subscribe authorization ───────────────────────────────────────────

    [Fact]
    public async Task Subscribe_RegularUser_WithSubscribePermission_Succeeds() {
        await _fixture.CreateUserAsync("sub-allowed", "pass");
        await _fixture.GrantPermissionAsync("sub-allowed", "sub-allowed.*",
            [QueueOperation.Subscribe, QueueOperation.Publish, QueueOperation.CreateQueue]);

        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.CreateQueueAsync("sub-allowed.events");

        await using var client = await _fixture.ConnectAsync("sub-allowed", "pass");

        await using var sub = await client.SubscribeAsync<object>(
            "sub-allowed.events",
            _ => Task.CompletedTask
        );

        Assert.NotNull(sub);
    }

    [Fact]
    public async Task Subscribe_RegularUser_WithoutPermission_Throws() {
        await _fixture.CreateUserAsync("sub-denied", "pass");
        // No permissions granted

        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.CreateQueueAsync("restricted-sub-queue");

        await using var client = await _fixture.ConnectAsync("sub-denied", "pass");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SubscribeAsync<object>("restricted-sub-queue", _ => Task.CompletedTask)
        );
    }

    // ─── Queue management authorization ────────────────────────────────────

    [Fact]
    public async Task CreateQueue_RegularUser_WithPermission_Succeeds() {
        await _fixture.CreateUserAsync("cq-allowed", "pass");
        await _fixture.GrantPermissionAsync("cq-allowed", "cq-allowed.*",
            [QueueOperation.CreateQueue]);

        await using var client = await _fixture.ConnectAsync("cq-allowed", "pass");

        await client.CreateQueueAsync("cq-allowed.myqueue");
    }

    [Fact]
    public async Task CreateQueue_RegularUser_WithoutPermission_Throws() {
        await _fixture.CreateUserAsync("cq-denied", "pass");

        await using var client = await _fixture.ConnectAsync("cq-denied", "pass");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.CreateQueueAsync("restricted-create-queue")
        );
    }

    [Fact]
    public async Task DeleteQueue_RegularUser_WithPermission_Succeeds() {
        await _fixture.CreateUserAsync("dq-allowed", "pass");
        await _fixture.GrantPermissionAsync("dq-allowed", "dq-allowed.*",
            [QueueOperation.CreateQueue, QueueOperation.DeleteQueue]);

        await using var client = await _fixture.ConnectAsync("dq-allowed", "pass");

        await client.CreateQueueAsync("dq-allowed.temp");
        await client.DeleteQueueAsync("dq-allowed.temp");
    }

    // ─── ListQueues filtering ──────────────────────────────────────────────

    [Fact]
    public async Task ListQueues_Superuser_ReturnsAllQueues() {
        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.CreateQueueAsync("lq-all-q1");
        await su.CreateQueueAsync("lq-all-q2");

        var queues = await su.ListQueuesAsync();

        Assert.Contains("lq-all-q1", queues);
        Assert.Contains("lq-all-q2", queues);
    }

    [Fact]
    public async Task ListQueues_RegularUser_ReturnsOnlyOwnQueues() {
        await _fixture.CreateUserAsync("lq-user", "pass");
        await _fixture.GrantPermissionAsync("lq-user", "lq-user.*",
            [QueueOperation.ListQueues, QueueOperation.CreateQueue]);

        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.CreateQueueAsync("lq-user.mine");
        await su.CreateQueueAsync("lq-other.not-mine");

        await using var client = await _fixture.ConnectAsync("lq-user", "pass");

        var queues = await client.ListQueuesAsync();

        Assert.Contains("lq-user.mine", queues);
        Assert.DoesNotContain("lq-other.not-mine", queues);
    }

    [Fact]
    public async Task ListQueues_RegularUser_MultiplePatterns_UnionResult() {
        await _fixture.CreateUserAsync("lq-multi", "pass");
        await _fixture.GrantPermissionAsync("lq-multi", "lq-multi.a.*",
            [QueueOperation.ListQueues]);
        await _fixture.GrantPermissionAsync("lq-multi", "lq-multi.b.*",
            [QueueOperation.ListQueues]);

        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.CreateQueueAsync("lq-multi.a.one");
        await su.CreateQueueAsync("lq-multi.b.two");
        await su.CreateQueueAsync("lq-multi.c.hidden");

        await using var client = await _fixture.ConnectAsync("lq-multi", "pass");

        var queues = await client.ListQueuesAsync();

        Assert.Contains("lq-multi.a.one", queues);
        Assert.Contains("lq-multi.b.two", queues);
        Assert.DoesNotContain("lq-multi.c.hidden", queues);
    }

    // ─── Permission scope (glob isolation) ────────────────────────────────

    [Fact]
    public async Task Publish_UserWithPattern_CannotPublishOutsidePattern() {
        await _fixture.CreateUserAsync("scope-user", "pass");
        await _fixture.GrantPermissionAsync("scope-user", "scope-user.*",
            [QueueOperation.Publish]);

        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.CreateQueueAsync("other.tenant.queue");

        await using var client = await _fixture.ConnectAsync("scope-user", "pass");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.PublishAsync("other.tenant.queue", new { Data = "x" })
        );
    }
}
