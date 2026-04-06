using VibeMQ.Client;

namespace VibeMQ.Tests.Integration;

/// <summary>
/// Integration tests for connecting via connection string (URL and key=value).
/// </summary>
public sealed class ConnectionStringTests : IClassFixture<NoAuthBrokerFixture> {
    private readonly NoAuthBrokerFixture _fixture;

    public ConnectionStringTests(NoAuthBrokerFixture fixture) {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConnectAsync_URL_connects_to_broker() {
        var connectionString = $"vibemq://127.0.0.1:{_fixture.Port}";
        await using var client = await VibeMQClient.ConnectAsync(connectionString);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_keyvalue_connects_to_broker() {
        var connectionString = $"Host=127.0.0.1;Port={_fixture.Port}";
        await using var client = await VibeMQClient.ConnectAsync(connectionString);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_URL_with_compression_none_connects() {
        var connectionString = $"vibemq://127.0.0.1:{_fixture.Port}?compression=none";
        await using var client = await VibeMQClient.ConnectAsync(connectionString);
        Assert.True(client.IsConnected);
    }
}
