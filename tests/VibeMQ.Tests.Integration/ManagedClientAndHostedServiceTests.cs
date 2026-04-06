using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VibeMQ.Client;
using VibeMQ.Client.DependencyInjection;
using VibeMQ.Interfaces;

namespace VibeMQ.Tests.Integration;

/// <summary>
/// Integration tests for ManagedVibeMQClient and MessageHandlerHostedService with a real broker.
/// </summary>
public sealed class ManagedClientAndHostedServiceTests : IClassFixture<TestBrokerFixture> {
    private readonly TestBrokerFixture _fixture;

    public ManagedClientAndHostedServiceTests(TestBrokerFixture fixture) {
        _fixture = fixture;
    }

    [Fact]
    public async Task ManagedVibeMQClient_ConnectsAndPublishes_WhenResolvedFromDI() {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVibeMQClient(settings => {
            settings.Host = "127.0.0.1";
            settings.Port = _fixture.Port;
            settings.ClientOptions = new ClientOptions {
                Username = TestBrokerFixture.Username,
                Password = TestBrokerFixture.Password,
                CommandTimeout = IntegrationTestTimeouts.ClientCommandTimeout,
                ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 }
            };
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IVibeMQClient>();

        Assert.False(client.IsConnected, "Lazy: not connected before first use.");

        await client.CreateQueueAsync("managed-client-queue");
        await client.PublishAsync("managed-client-queue", new { Value = 42 });

        Assert.True(client.IsConnected);

        if (client is IDisposable disposable) {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task MessageHandlerHostedService_SubscribesAndReceivesMessage_OverRealBroker() {
        var received = new ConcurrentBag<IntegrationDiMessage>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new IntegrationMessageSink(received));
        services.AddVibeMQClient(settings => {
            settings.Host = "127.0.0.1";
            settings.Port = _fixture.Port;
            settings.ClientOptions = new ClientOptions {
                Username = TestBrokerFixture.Username,
                Password = TestBrokerFixture.Password,
                CommandTimeout = IntegrationTestTimeouts.ClientCommandTimeout,
                ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 }
            };
        });
        services.AddMessageHandler<IntegrationDiMessage, IntegrationDiHandler>();
        services.AddMessageHandlerSubscriptions();

        var host = new HostBuilder()
            .ConfigureServices((_, s) => {
                foreach (var sd in services) {
                    s.Add(sd);
                }
            })
            .Build();

        await host.StartAsync(CancellationToken.None);

        try {
            var client = host.Services.GetRequiredService<IVibeMQClient>();
            await client.CreateQueueAsync("integration-di-queue");
            await Task.Delay(IntegrationTestTimeouts.HostedServiceSubscriptionDelayMs, CancellationToken.None); // allow subscription to be active
            await client.PublishAsync("integration-di-queue", new IntegrationDiMessage { Id = 1, Text = "hello" });

            await Task.Delay(IntegrationTestTimeouts.HostedServiceDeliveryDelayMs, CancellationToken.None); // allow delivery

            Assert.Single(received);
            var msg = received.Single();
            Assert.Equal(1, msg.Id);
            Assert.Equal("hello", msg.Text);
        } finally {
            await host.StopAsync(CancellationToken.None);
        }
    }
}

public sealed class IntegrationDiMessage {
    public int Id { get; set; }
    public string? Text { get; set; }
}

sealed class IntegrationMessageSink(ConcurrentBag<IntegrationDiMessage> received) {
    public void Add(IntegrationDiMessage message) => received.Add(message);
}

[Attributes.Queue("integration-di-queue")]
sealed class IntegrationDiHandler(IntegrationMessageSink sink) : IMessageHandler<IntegrationDiMessage> {
    public Task HandleAsync(IntegrationDiMessage message, CancellationToken cancellationToken) {
        sink.Add(message);
        return Task.CompletedTask;
    }
}
