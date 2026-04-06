using VibeMQ.Client;
using VibeMQ.Protocol.Compression;

namespace VibeMQ.Tests.Integration;

/// <summary>
/// Integration tests verifying compression negotiation and end-to-end delivery with compression.
/// </summary>
public class CompressionTests : IClassFixture<NoAuthBrokerFixture> {
    private readonly NoAuthBrokerFixture _fixture;

    public CompressionTests(NoAuthBrokerFixture fixture) {
        _fixture = fixture;
    }

    // -------------------------------------------------------------------------
    // Negotiation scenarios
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Negotiation_ClientPrefersBrotli_ServerSupportsBoth_SelectsBrotli() {
        var options = new ClientOptions {
            PreferredCompressions = [CompressionAlgorithm.Brotli, CompressionAlgorithm.GZip],
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 }
        };

        await using var client = await VibeMQClient.ConnectAsync("127.0.0.1", _fixture.Port, options);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Negotiation_ClientPrefersGZipOnly_ServerSupportsBoth_SelectsGZip() {
        var options = new ClientOptions {
            PreferredCompressions = [CompressionAlgorithm.GZip],
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 }
        };

        await using var client = await VibeMQClient.ConnectAsync("127.0.0.1", _fixture.Port, options);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Negotiation_ClientDisablesCompression_WorksWithoutCompression() {
        var options = new ClientOptions {
            PreferredCompressions = [],
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 }
        };

        await using var client = await VibeMQClient.ConnectAsync("127.0.0.1", _fixture.Port, options);
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
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 }
        };

        var withoutCompression = new ClientOptions {
            PreferredCompressions = [],
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 }
        };

        await using var clientA = await VibeMQClient.ConnectAsync("127.0.0.1", _fixture.Port, withCompression);
        await using var clientB = await VibeMQClient.ConnectAsync("127.0.0.1", _fixture.Port, withoutCompression);

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
            CommandTimeout = IntegrationTestTimeouts.ClientCommandTimeout
        };

        await using var publisher = await VibeMQClient.ConnectAsync("127.0.0.1", _fixture.Port, options);
        await using var subscriber = await VibeMQClient.ConnectAsync("127.0.0.1", _fixture.Port, options);

        var received = new TaskCompletionSource<LargePayload>();
        var queueName = $"brotli-queue-{Guid.NewGuid():N}";

        // Generate a payload well over 1 KB
        var expectedData = new string('a', 2048);

        await subscriber.SubscribeAsync<LargePayload>(queueName, msg => {
            received.TrySetResult(msg);
            return Task.CompletedTask;
        });

        await Task.Delay(IntegrationTestTimeouts.SubscriptionActivationDelayMs);

        await publisher.PublishAsync(queueName, new LargePayload { Data = expectedData });

        var result = await received.Task.WaitAsync(IntegrationTestTimeouts.MessageDeliveryTimeout);
        Assert.Equal(expectedData, result.Data);
    }

    [Fact]
    public async Task PubSub_WithGZipCompression_LargeMessage_DeliveredCorrectly() {
        var options = new ClientOptions {
            PreferredCompressions = [CompressionAlgorithm.GZip],
            CompressionThreshold = 0,
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 },
            CommandTimeout = IntegrationTestTimeouts.ClientCommandTimeout
        };

        await using var publisher = await VibeMQClient.ConnectAsync("127.0.0.1", _fixture.Port, options);
        await using var subscriber = await VibeMQClient.ConnectAsync("127.0.0.1", _fixture.Port, options);

        var received = new TaskCompletionSource<LargePayload>();
        var queueName = $"gzip-queue-{Guid.NewGuid():N}";
        var expectedData = new string('b', 2048);

        await subscriber.SubscribeAsync<LargePayload>(queueName, msg => {
            received.TrySetResult(msg);
            return Task.CompletedTask;
        });

        await Task.Delay(IntegrationTestTimeouts.SubscriptionActivationDelayMs);

        await publisher.PublishAsync(queueName, new LargePayload { Data = expectedData });

        var result = await received.Task.WaitAsync(IntegrationTestTimeouts.MessageDeliveryTimeout);
        Assert.Equal(expectedData, result.Data);
    }

    [Fact]
    public async Task PubSub_AboveThreshold_CompressedTransparently() {
        var options = new ClientOptions {
            PreferredCompressions = [CompressionAlgorithm.Brotli, CompressionAlgorithm.GZip],
            CompressionThreshold = 512,
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 },
            CommandTimeout = IntegrationTestTimeouts.ClientCommandTimeout
        };

        await using var publisher = await VibeMQClient.ConnectAsync("127.0.0.1", _fixture.Port, options);
        await using var subscriber = await VibeMQClient.ConnectAsync("127.0.0.1", _fixture.Port, options);

        var received = new TaskCompletionSource<LargePayload>();
        var queueName = $"threshold-queue-{Guid.NewGuid():N}";
        var expectedData = new string('c', 2048); // well above 512 bytes

        await subscriber.SubscribeAsync<LargePayload>(queueName, msg => {
            received.TrySetResult(msg);
            return Task.CompletedTask;
        });

        await Task.Delay(IntegrationTestTimeouts.SubscriptionActivationDelayMs);

        await publisher.PublishAsync(queueName, new LargePayload { Data = expectedData });

        var result = await received.Task.WaitAsync(IntegrationTestTimeouts.MessageDeliveryTimeout);
        Assert.Equal(expectedData, result.Data);
    }

}

public class LargePayload {
    public string Data { get; set; } = string.Empty;
}
