using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using VibeMQ.Client;
using VibeMQ.Enums;
using VibeMQ.Server;

namespace VibeMQ.Benchmarks;

/// <summary>
/// Load test: runs broker in-process, multiple publishers and subscribers over TCP,
/// measures throughput (messages per second) and reports results.
/// Run with: dotnet run -c Release -- --filter *LoadTest* or dotnet run -c Release -- load
/// </summary>
public static class LoadTest {
    private const string LOAD_QUEUE = "load-queue";

    /// <summary>
    /// Runs the load test with given parameters and prints results to console.
    /// </summary>
    public static async Task RunAsync(
        int publisherCount = 4,
        int subscriberCount = 2,
        int durationSeconds = 10,
        int messagePayloadSize = 128,
        CancellationToken cancellationToken = default)
    {
        var port = GetFreePort();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = cts.Token;

        // Build broker without auth, high limits for load test
        var broker = BrokerBuilder.Create()
            .UsePort(port)
            .UseMaxConnections(500)
            .UseMaxMessageSize(1_048_576)
            .ConfigureQueues(options => {
                options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
                options.MaxQueueSize = 1_000_000;
                options.EnableAutoCreate = true;
            })
            .ConfigureRateLimiting(options => {
                options.Enabled = false;
            })
            .UseLoggerFactory(NullLoggerFactory.Instance)
            .Build();

        var runTask = broker.RunAsync(token);
        await WaitForPortAsync(port, token).ConfigureAwait(false);

        var totalPublished = 0L;
        var totalReceived = 0L;
        var payload = new string('x', Math.Max(0, messagePayloadSize));

        // Subscribers
        var subscriberTasks = new List<Task>();
        for (var i = 0; i < subscriberCount; i++) {
            var idx = i;
            subscriberTasks.Add(Task.Run(async () => {
                await using var client = await VibeMQClient.ConnectAsync("127.0.0.1", port, new ClientOptions {
                    KeepAliveInterval = TimeSpan.FromSeconds(60),
                    CommandTimeout = TimeSpan.FromSeconds(30),
                }, null, token).ConfigureAwait(false);

                await client.SubscribeAsync<string>(LOAD_QUEUE, _ => {
                    Interlocked.Increment(ref totalReceived);
                    return Task.CompletedTask;
                }, token).ConfigureAwait(false);

                await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
            }, token));
        }

        await Task.Delay(500, token).ConfigureAwait(false); // Let subscribers register

        // Publishers
        var sw = Stopwatch.StartNew();
        var duration = TimeSpan.FromSeconds(durationSeconds);
        var publisherTasks = Enumerable.Range(0, publisherCount).Select(_ => Task.Run(async () => {
            await using var client = await VibeMQClient.ConnectAsync("127.0.0.1", port, new ClientOptions {
                KeepAliveInterval = TimeSpan.FromSeconds(60),
                CommandTimeout = TimeSpan.FromSeconds(30),
            }, null, token).ConfigureAwait(false);

            while (sw.Elapsed < duration && !token.IsCancellationRequested) {
                await client.PublishAsync(LOAD_QUEUE, payload, token).ConfigureAwait(false);
                Interlocked.Increment(ref totalPublished);
            }
        }, token)).ToList();

        await Task.WhenAll(publisherTasks).ConfigureAwait(false);
        sw.Stop();

        // Allow in-flight messages to be delivered
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline) {
            var r = Interlocked.Read(ref totalReceived);
            var p = Interlocked.Read(ref totalPublished);
            if (r >= p) break;
            await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
        }

        cts.Cancel();
        try {
            await broker.StopAsync(CancellationToken.None).ConfigureAwait(false);
        } catch { /* ignore */ }

        try {
            await runTask.ConfigureAwait(false);
        } catch (OperationCanceledException) { /* expected */ }

        try {
            await Task.WhenAll(subscriberTasks).ConfigureAwait(false);
        } catch (OperationCanceledException) { /* expected */ }
        catch (AggregateException ae) when (ae.InnerExceptions.All(e => e is OperationCanceledException)) { /* expected */ }

        var elapsedSec = sw.Elapsed.TotalSeconds;
        var totalPublishedFinal = Interlocked.Read(ref totalPublished);
        var totalReceivedFinal = Interlocked.Read(ref totalReceived);
        var publishRate = elapsedSec > 0 ? totalPublishedFinal / elapsedSec : 0;
        var receiveRate = elapsedSec > 0 ? totalReceivedFinal / elapsedSec : 0;

        Console.WriteLine();
        Console.WriteLine("========== VibeMQ Load Test Results ==========");
        Console.WriteLine($"  Publishers:       {publisherCount}");
        Console.WriteLine($"  Subscribers:      {subscriberCount}");
        Console.WriteLine($"  Duration:         {durationSeconds}s (actual: {elapsedSec:F2}s)");
        Console.WriteLine($"  Payload size:    {messagePayloadSize} bytes");
        Console.WriteLine($"  Messages sent:   {totalPublishedFinal:N0}");
        Console.WriteLine($"  Messages received: {totalReceivedFinal:N0}");
        Console.WriteLine($"  Throughput (send):   {publishRate:N0} msg/s");
        Console.WriteLine($"  Throughput (receive): {receiveRate:N0} msg/s");
        Console.WriteLine("===============================================");
        Console.WriteLine();
    }

    private static int GetFreePort() {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForPortAsync(int port, CancellationToken cancellationToken) {
        for (var i = 0; i < 50; i++) {
            try {
                using var c = new TcpClient();
                await c.ConnectAsync(IPAddress.Loopback, port, cancellationToken).ConfigureAwait(false);
                return;
            } catch {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
        throw new InvalidOperationException($"Port {port} did not become ready in time.");
    }
}
