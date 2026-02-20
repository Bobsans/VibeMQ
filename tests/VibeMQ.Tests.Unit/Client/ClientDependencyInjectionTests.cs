using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VibeMQ.Client;
using VibeMQ.Client.DependencyInjection;

namespace VibeMQ.Tests.Unit.Client;

public class ClientDependencyInjectionTests {
    [Fact]
    public void AddVibeMQClient_Registers_IVibeMQClient_And_IVibeMQClientFactory() {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVibeMQClient(settings => {
            settings.Host = "localhost";
            settings.Port = 8080;
        });
        var provider = services.BuildServiceProvider();

        var client = provider.GetService<IVibeMQClient>();
        var factory = provider.GetService<IVibeMQClientFactory>();

        Assert.NotNull(client);
        Assert.NotNull(factory);
        Assert.False(client.IsConnected, "Client should not be connected before first use (lazy connect).");
    }

    [Fact]
    public void IVibeMQClient_Is_Singleton() {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVibeMQClient();
        var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IVibeMQClient>();
        var second = provider.GetRequiredService<IVibeMQClient>();

        Assert.Same(first, second);
    }
}
