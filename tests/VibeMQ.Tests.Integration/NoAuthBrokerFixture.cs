using System.Net;
using System.Net.Sockets;
using VibeMQ.Server;

namespace VibeMQ.Tests.Integration;

/// <summary>
/// Shared fixture that starts a broker without authentication.
/// </summary>
public sealed class NoAuthBrokerFixture : IAsyncLifetime, IDisposable {
    private BrokerServer? _server;
    private Task? _serverTask;
    private CancellationTokenSource? _cts;

    public int Port { get; private set; }

    public async Task InitializeAsync() {
        Port = GetFreePort();
        _cts = new CancellationTokenSource();

        _server = BrokerBuilder.Create()
            .UsePort(Port)
            .UseMaxConnections(50)
            .ConfigureRateLimiting(o => o.Enabled = false)
            .Build();

        _serverTask = _server.RunAsync(_cts.Token);
        await WaitForPortAsync(Port, _serverTask, IntegrationTestTimeouts.BrokerStartupTimeout);
    }

    public async Task DisposeAsync() {
        if (_cts is not null) {
            await _cts.CancelAsync();
        }

        if (_serverTask is not null) {
            try {
                await _serverTask.WaitAsync(IntegrationTestTimeouts.BrokerShutdownWaitTimeout);
            } catch {
                // Expected during shutdown
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

    public void Dispose() {
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static int GetFreePort() {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task WaitForPortAsync(int port, Task serverTask, TimeSpan timeout) {
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.Token.IsCancellationRequested) {
            if (serverTask.IsCompleted) {
                await serverTask;
            }

            try {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync("127.0.0.1", port, cts.Token);
                return;
            } catch (SocketException) {
                await Task.Delay(IntegrationTestTimeouts.PortProbeRetryDelayMs, cts.Token);
            }
        }

        throw new TimeoutException($"Server did not start listening on port {port} within {timeout.TotalSeconds}s.");
    }
}
