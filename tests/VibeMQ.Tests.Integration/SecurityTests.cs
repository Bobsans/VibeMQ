using VibeMQ.Client;

namespace VibeMQ.Tests.Integration;

public class SecurityTests : IClassFixture<TestBrokerFixture> {
    private readonly TestBrokerFixture _fixture;

    public SecurityTests(TestBrokerFixture fixture) {
        _fixture = fixture;
    }

    [Fact]
    public async Task Connect_WithInvalidCredentials_ThrowsOrFails() {
        var options = new ClientOptions {
            Username = "wrong-user",
            Password = "wrong-pass",
            CommandTimeout = IntegrationTestTimeouts.ClientCommandTimeout,
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => VibeMQClient.ConnectAsync("127.0.0.1", _fixture.Port, options)
        );
    }

    [Fact]
    public async Task Connect_WithValidCredentials_Succeeds() {
        await using var client = await _fixture.CreateClientAsync(authenticate: true);

        Assert.True(client.IsConnected);
    }
}
