using System.Net;
using System.Net.Sockets;
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
        // Let the OS assign a free port to avoid collisions and TIME_WAIT issues
        Port = GetFreePort();
        _cts = new CancellationTokenSource();

        _server = BrokerBuilder.Create()
            .UsePort(Port)
            .UseAuthentication(AUTH_TOKEN)
            .UseMaxConnections(100)
            .ConfigureRateLimiting(o => o.Enabled = false)
            .Build();

        _serverTask = _server.RunAsync(_cts.Token);

        // Wait until the server is actually listening (or fail fast if it crashed)
        await WaitForPortAsync(Port, _serverTask, TimeSpan.FromSeconds(10));
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

    /// <summary>
    /// Asks the OS to assign an available ephemeral port, avoiding collisions.
    /// </summary>
    private static int GetFreePort() {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Polls until the port is reachable or the server task faults (whichever comes first).
    /// </summary>
    private static async Task WaitForPortAsync(int port, Task serverTask, TimeSpan timeout) {
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.Token.IsCancellationRequested) {
            // Fail fast if the server crashed during startup
            if (serverTask.IsCompleted) {
                await serverTask; // rethrows the exception
            }

            try {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync("127.0.0.1", port, cts.Token);
                return; // Port is open — server is ready
            } catch (SocketException) {
                await Task.Delay(50, cts.Token);
            }
        }

        throw new TimeoutException($"Server did not start listening on port {port} within {timeout.TotalSeconds}s.");
    }
}
