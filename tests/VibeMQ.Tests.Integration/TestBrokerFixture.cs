using VibeMQ.Client;
using VibeMQ.Server;

namespace VibeMQ.Tests.Integration;

/// <summary>
/// Shared test fixture that starts a broker on a random port for integration tests.
/// </summary>
public sealed class TestBrokerFixture : IAsyncLifetime {
    private const string AUTH_TOKEN = "test-secret-token";

    private BrokerServer? _server;
    private Task? _serverTask;
    private CancellationTokenSource? _cts;

    public int Port { get; private set; }

    public async Task InitializeAsync() {
        Port = Random.Shared.Next(30_000, 60_000);
        _cts = new CancellationTokenSource();

        _server = BrokerBuilder.Create()
            .UsePort(Port)
            .UseAuthentication(AUTH_TOKEN)
            .UseMaxConnections(100)
            .ConfigureRateLimiting(o => o.Enabled = false)
            .Build();

        _serverTask = _server.RunAsync(_cts.Token);

        // Give the server time to start
        await Task.Delay(300);
    }

    public async Task<VibeMQClient> CreateClientAsync(bool authenticate = true) {
        var options = new ClientOptions {
            AuthToken = authenticate ? AUTH_TOKEN : null,
            CommandTimeout = TimeSpan.FromSeconds(5),
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 },
        };

        var client = await VibeMQClient.ConnectAsync("127.0.0.1", Port, options);
        return client;
    }

    public async Task DisposeAsync() {
        if (_cts is not null) {
            await _cts.CancelAsync();
        }

        if (_serverTask is not null) {
            try {
                await _serverTask.WaitAsync(TimeSpan.FromSeconds(5));
            } catch {
                // Server task may throw on shutdown
            }
        }

        if (_server is not null) {
            try {
                await _server.DisposeAsync();
            } catch {
                // Ignore dispose errors during test cleanup
            }
        }

        _cts?.Dispose();
    }
}
