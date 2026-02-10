using VibeMQ.Client;

namespace VibeMQ.Tests.Integration;

public class SecurityTests : IAsyncLifetime {
    private readonly TestBrokerFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Connect_WithInvalidToken_ThrowsOrFails() {
        var options = new ClientOptions {
            AuthToken = "wrong-token",
            CommandTimeout = TimeSpan.FromSeconds(3),
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 },
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => VibeMQClient.ConnectAsync("127.0.0.1", _fixture.Port, options)
        );
    }

    [Fact]
    public async Task Connect_WithValidToken_Succeeds() {
        await using var client = await _fixture.CreateClientAsync(authenticate: true);

        Assert.True(client.IsConnected);
    }
}
