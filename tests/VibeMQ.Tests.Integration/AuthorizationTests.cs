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

    // ─── Admin commands (superuser) ─────────────────────────────────────────

    [Fact]
    public async Task Admin_CreateUser_Superuser_Succeeds() {
        await using var su = await _fixture.ConnectAsSuperuserAsync();

        await su.CreateUserAsync("admin-created", "Secret123");

        var users = await su.ListUsersAsync();
        var created = users.FirstOrDefault(u => u.Username == "admin-created");
        Assert.NotNull(created);
        Assert.False(created.IsSuperuser);

        await using var client = await _fixture.ConnectAsync("admin-created", "Secret123");
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Admin_CreateUser_Duplicate_Throws() {
        await _fixture.CreateUserAsync("existing-user", "pass");

        await using var su = await _fixture.ConnectAsSuperuserAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => su.CreateUserAsync("existing-user", "other-pass")
        );
    }

    [Theory]
    [InlineData("bad user")]
    [InlineData("bad/user")]
    [InlineData("bad@user")]
    public async Task Admin_CreateUser_InvalidUsername_Throws(string username) {
        await using var su = await _fixture.ConnectAsSuperuserAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => su.CreateUserAsync(username, "pass")
        );
    }

    [Fact]
    public async Task Admin_DeleteUser_Superuser_Succeeds() {
        await _fixture.CreateUserAsync("to-delete", "pass");

        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.DeleteUserAsync("to-delete");

        var users = await su.ListUsersAsync();
        Assert.DoesNotContain(users, u => u.Username == "to-delete");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _fixture.ConnectAsync("to-delete", "pass")
        );
    }

    [Fact]
    public async Task Admin_DeleteUser_NonExistent_Throws() {
        await using var su = await _fixture.ConnectAsSuperuserAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => su.DeleteUserAsync("no-such-user")
        );
    }

    [Fact]
    public async Task Admin_ChangePassword_Superuser_Succeeds() {
        await _fixture.CreateUserAsync("pw-user", "old-pass");

        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.ChangePasswordAsync("pw-user", "new-pass");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _fixture.ConnectAsync("pw-user", "old-pass")
        );
        await using var client = await _fixture.ConnectAsync("pw-user", "new-pass");
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Admin_GrantPermission_Superuser_Succeeds() {
        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.CreateUserAsync("grant-user", "pass");
        await su.GrantPermissionAsync("grant-user", "grant-user.*",
            [QueueOperation.Publish, QueueOperation.CreateQueue]);

        await su.CreateQueueAsync("grant-user.orders");
        await using var client = await _fixture.ConnectAsync("grant-user", "pass");
        await client.PublishAsync("grant-user.orders", new { Id = 1 });
    }

    [Fact]
    public async Task Admin_RevokePermission_Superuser_Succeeds() {
        await _fixture.CreateUserAsync("revoke-user", "pass");
        await _fixture.GrantPermissionAsync("revoke-user", "revoke-user.*",
            [QueueOperation.Publish, QueueOperation.CreateQueue]);

        await using var su = await _fixture.ConnectAsSuperuserAsync();
        await su.CreateQueueAsync("revoke-user.data");
        await su.RevokePermissionAsync("revoke-user", "revoke-user.*");

        await using var client = await _fixture.ConnectAsync("revoke-user", "pass");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.PublishAsync("revoke-user.data", new { X = 1 })
        );
    }

    [Fact]
    public async Task Admin_GetUserPermissions_Superuser_ReturnsGranted() {
        await _fixture.CreateUserAsync("perm-user", "pass");
        await _fixture.GrantPermissionAsync("perm-user", "perm-user.a.*", [QueueOperation.Publish]);
        await _fixture.GrantPermissionAsync("perm-user", "perm-user.b.*", [QueueOperation.Subscribe]);

        await using var su = await _fixture.ConnectAsSuperuserAsync();
        var perms = await su.GetUserPermissionsAsync("perm-user");

        Assert.Equal(2, perms.Count);
        var a = perms.FirstOrDefault(p => p.QueuePattern == "perm-user.a.*");
        var b = perms.FirstOrDefault(p => p.QueuePattern == "perm-user.b.*");
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Contains("Publish", a.Operations);
        Assert.Contains("Subscribe", b.Operations);
    }

    [Fact]
    public async Task Admin_GetUserPermissions_RegularUser_CanReadOnlyOwnPermissions() {
        await _fixture.CreateUserAsync("perm-self", "pass");
        await _fixture.CreateUserAsync("perm-other", "pass");
        await _fixture.GrantPermissionAsync("perm-self", "perm-self.*", [QueueOperation.Publish]);
        await _fixture.GrantPermissionAsync("perm-other", "perm-other.*", [QueueOperation.Subscribe]);

        await using var client = await _fixture.ConnectAsync("perm-self", "pass");

        var own = await client.GetUserPermissionsAsync("perm-self");
        Assert.Single(own);
        Assert.Equal("perm-self.*", own[0].QueuePattern);
        Assert.Contains("Publish", own[0].Operations);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetUserPermissionsAsync("perm-other")
        );
    }

    [Fact]
    public async Task Admin_GetUserPermissions_NoUser_ReturnsEmpty() {
        await using var su = await _fixture.ConnectAsSuperuserAsync();

        var perms = await su.GetUserPermissionsAsync("nonexistent");

        Assert.Empty(perms);
    }

    [Fact]
    public async Task Admin_ListUsers_Superuser_ReturnsAll() {
        await _fixture.CreateUserAsync("list-a", "pass");
        await _fixture.CreateUserAsync("list-b", "pass");

        await using var su = await _fixture.ConnectAsSuperuserAsync();
        var users = await su.ListUsersAsync();

        Assert.Contains(users, u => u.Username == AuthBrokerFixture.SuperuserUsername);
        Assert.Contains(users, u => u.Username == "list-a");
        Assert.Contains(users, u => u.Username == "list-b");
    }

    [Fact]
    public async Task Admin_RegularUser_CannotCallAdminCommand_Throws() {
        await _fixture.CreateUserAsync("regular", "pass");
        await _fixture.GrantPermissionAsync("regular", "regular.*", [QueueOperation.CreateQueue]);

        await using var client = await _fixture.ConnectAsync("regular", "pass");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.CreateUserAsync("hacker-created", "pwd")
        );
    }
}
