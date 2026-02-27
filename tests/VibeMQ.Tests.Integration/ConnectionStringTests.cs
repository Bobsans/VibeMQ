using System.Net;
using System.Net.Sockets;
using VibeMQ.Client;
using VibeMQ.Server;

namespace VibeMQ.Tests.Integration;

/// <summary>
/// Integration tests for connecting via connection string (URL and key=value).
/// </summary>
public sealed class ConnectionStringTests : IAsyncLifetime {
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
        await WaitForPortAsync(Port, _serverTask, TimeSpan.FromSeconds(10));
    }

    public async Task DisposeAsync() {
        if (_cts is not null) {
            await _cts.CancelAsync();
        }
        if (_serverTask is not null) {
            try {
                await _serverTask.WaitAsync(TimeSpan.FromSeconds(5));
            } catch {
                /* expected */
            }
        }
        if (_server is not null) {
            try {
                await _server.DisposeAsync();
            } catch {
                /* ignore */
            }
        }
        _cts?.Dispose();
    }

    [Fact]
    public async Task ConnectAsync_URL_connects_to_broker() {
        var connectionString = $"vibemq://127.0.0.1:{Port}";
        await using var client = await VibeMQClient.ConnectAsync(connectionString);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_keyvalue_connects_to_broker() {
        var connectionString = $"Host=127.0.0.1;Port={Port}";
        await using var client = await VibeMQClient.ConnectAsync(connectionString);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_URL_with_compression_none_connects() {
        var connectionString = $"vibemq://127.0.0.1:{Port}?compression=none";
        await using var client = await VibeMQClient.ConnectAsync(connectionString);
        Assert.True(client.IsConnected);
    }

    private static int GetFreePort() {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return port;
    }

    private static async Task WaitForPortAsync(int port, Task serverTask, TimeSpan timeout) {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline) {
            if (serverTask.IsFaulted) {
                await serverTask;
            }
            try {
                using var c = new System.Net.Sockets.TcpClient();
                await c.ConnectAsync("127.0.0.1", port);
                return;
            } catch {
                await Task.Delay(50);
            }
        }
        throw new TimeoutException($"Port {port} did not become ready in time.");
    }
}
