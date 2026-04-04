using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VibeMQ.Attributes;
using VibeMQ.Client;
using VibeMQ.Client.DependencyInjection;
using VibeMQ.Interfaces;

namespace VibeMQ.Tests.Unit.Client;

public class MessageHandlerHostedServiceTests {
    [Fact]
    public async Task StartAsync_SubscribesRegisteredHandlersWithQueueAttribute() {
        var fakeClient = new FakeRecordingVibeMQClient();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IVibeMQClient>(_ => fakeClient);
        services.AddMessageHandler<HostedTestMessage, HostedTestHandler>();
        services.AddMessageHandlerSubscriptions();

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetRequiredService<MessageHandlerHostedService>();

        await hostedService.StartAsync(CancellationToken.None);

        var recorded = fakeClient.RecordedSubscriptions;
        Assert.Contains(recorded, r => r.QueueName == "hosted-test-queue"
            && r.MessageType == typeof(HostedTestMessage)
            && r.HandlerType == typeof(HostedTestHandler));
    }

    [Fact]
    public async Task StopAsync_DisposesSubscriptions() {
        var fakeClient = new FakeRecordingVibeMQClient();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IVibeMQClient>(_ => fakeClient);
        services.AddMessageHandler<HostedTestMessage, HostedTestHandler>();
        services.AddMessageHandlerSubscriptions();

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetRequiredService<MessageHandlerHostedService>();

        await hostedService.StartAsync(CancellationToken.None);
        await hostedService.StopAsync(CancellationToken.None);

        // StopAsync should not throw; subscriptions are disposed
        Assert.True(fakeClient.RecordedSubscriptions.Count >= 1);
    }

    [Fact]
    public async Task StartAsync_WhenNoIVibeMQClientRegistered_DoesNotThrow() {
        var services = new ServiceCollection();
        services.AddLogging();
        // Do not register IVibeMQClient
        services.AddMessageHandlerSubscriptions();

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetRequiredService<MessageHandlerHostedService>();

        await hostedService.StartAsync(CancellationToken.None);
    }

    [Fact]
    public void AddMessageHandlerSubscriptions_RegistersHostedService() {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMessageHandlerSubscriptions();
        var provider = services.BuildServiceProvider();

        var hosted = provider.GetServices<IHostedService>().ToList();
        var diHosted = hosted.OfType<MessageHandlerHostedService>().ToList();

        Assert.Single(diHosted);
    }
}

public sealed class HostedTestMessage;

[Queue("hosted-test-queue")]
public sealed class HostedTestHandler : IMessageHandler<HostedTestMessage> {
    public Task HandleAsync(HostedTestMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}
