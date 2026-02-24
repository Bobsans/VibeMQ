using System.Net;
using System.Net.Sockets;
using System.Text;
using VibeMQ.Client;
using VibeMQ.Protocol.Compression;
using VibeMQ.Server;

namespace VibeMQ.Tests.Integration;

/// <summary>
/// Integration tests verifying compression negotiation and end-to-end delivery with compression.
/// </summary>
public class CompressionTests : IAsyncLifetime {
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
        if (_cts is not null) await _cts.CancelAsync();

        if (_serverTask is not null) {
            try { await _serverTask.WaitAsync(TimeSpan.FromSeconds(5)); } catch { /* expected */ }
        }

        if (_server is not null) {
            try { await _server.DisposeAsync(); } catch { /* ignore */ }
        }

        _cts?.Dispose();
    }

    // -------------------------------------------------------------------------
    // Negotiation scenarios
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Negotiation_ClientPrefersBrotli_ServerSupportsBoth_SelectsBrotli() {
        var options = new ClientOptions {
            PreferredCompressions = [CompressionAlgorithm.Brotli, CompressionAlgorithm.GZip],
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 },
        };

        await using var client = await VibeMQClient.ConnectAsync("127.0.0.1", Port, options);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Negotiation_ClientPrefersGZipOnly_ServerSupportsBoth_SelectsGZip() {
        var options = new ClientOptions {
            PreferredCompressions = [CompressionAlgorithm.GZip],
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 },
        };

        await using var client = await VibeMQClient.ConnectAsync("127.0.0.1", Port, options);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Negotiation_ClientDisablesCompression_WorksWithoutCompression() {
        var options = new ClientOptions {
            PreferredCompressions = [],
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 },
        };

        await using var client = await VibeMQClient.ConnectAsync("127.0.0.1", Port, options);
        Assert.True(client.IsConnected);
    }

    // -------------------------------------------------------------------------
    // Mixed scenarios
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Negotiation_MixedClients_BothConnectSuccessfully() {
        // Client with compression and client without compression connect to the same server
        var withCompression = new ClientOptions {
            PreferredCompressions = [CompressionAlgorithm.Brotli],
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 },
        };

        var withoutCompression = new ClientOptions {
            PreferredCompressions = [],
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 },
        };

        await using var clientA = await VibeMQClient.ConnectAsync("127.0.0.1", Port, withCompression);
        await using var clientB = await VibeMQClient.ConnectAsync("127.0.0.1", Port, withoutCompression);

        Assert.True(clientA.IsConnected);
        Assert.True(clientB.IsConnected);
    }

    // -------------------------------------------------------------------------
    // End-to-end with compression
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PubSub_WithBrotliCompression_LargeMessage_DeliveredCorrectly() {
        var options = new ClientOptions {
            PreferredCompressions = [CompressionAlgorithm.Brotli],
            CompressionThreshold = 0, // compress everything
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 },
            CommandTimeout = TimeSpan.FromSeconds(5),
        };

        await using var publisher = await VibeMQClient.ConnectAsync("127.0.0.1", Port, options);
        await using var subscriber = await VibeMQClient.ConnectAsync("127.0.0.1", Port, options);

        var received = new TaskCompletionSource<LargePayload>();
        var queueName = $"brotli-queue-{Guid.NewGuid():N}";

        // Generate a payload well over 1 KB
        var expectedData = new string('a', 2048);

        await subscriber.SubscribeAsync<LargePayload>(queueName, msg => {
            received.TrySetResult(msg);
            return Task.CompletedTask;
        });

        await Task.Delay(100);

        await publisher.PublishAsync(queueName, new LargePayload { Data = expectedData });

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(expectedData, result.Data);
    }

    [Fact]
    public async Task PubSub_WithGZipCompression_LargeMessage_DeliveredCorrectly() {
        var options = new ClientOptions {
            PreferredCompressions = [CompressionAlgorithm.GZip],
            CompressionThreshold = 0,
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 },
            CommandTimeout = TimeSpan.FromSeconds(5),
        };

        await using var publisher = await VibeMQClient.ConnectAsync("127.0.0.1", Port, options);
        await using var subscriber = await VibeMQClient.ConnectAsync("127.0.0.1", Port, options);

        var received = new TaskCompletionSource<LargePayload>();
        var queueName = $"gzip-queue-{Guid.NewGuid():N}";
        var expectedData = new string('b', 2048);

        await subscriber.SubscribeAsync<LargePayload>(queueName, msg => {
            received.TrySetResult(msg);
            return Task.CompletedTask;
        });

        await Task.Delay(100);

        await publisher.PublishAsync(queueName, new LargePayload { Data = expectedData });

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(expectedData, result.Data);
    }

    [Fact]
    public async Task PubSub_AboveThreshold_CompressedTransparently() {
        var options = new ClientOptions {
            PreferredCompressions = [CompressionAlgorithm.Brotli, CompressionAlgorithm.GZip],
            CompressionThreshold = 512,
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 },
            CommandTimeout = TimeSpan.FromSeconds(5),
        };

        await using var publisher = await VibeMQClient.ConnectAsync("127.0.0.1", Port, options);
        await using var subscriber = await VibeMQClient.ConnectAsync("127.0.0.1", Port, options);

        var received = new TaskCompletionSource<LargePayload>();
        var queueName = $"threshold-queue-{Guid.NewGuid():N}";
        var expectedData = new string('c', 2048); // well above 512 bytes

        await subscriber.SubscribeAsync<LargePayload>(queueName, msg => {
            received.TrySetResult(msg);
            return Task.CompletedTask;
        });

        await Task.Delay(100);

        await publisher.PublishAsync(queueName, new LargePayload { Data = expectedData });

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(expectedData, result.Data);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static int GetFreePort() {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
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
                await Task.Delay(50, cts.Token);
            }
        }

        throw new TimeoutException($"Server did not start listening on port {port} within {timeout.TotalSeconds}s.");
    }
}

public class LargePayload {
    public string Data { get; set; } = string.Empty;
}
