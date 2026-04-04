using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VibeMQ.Client;
using VibeMQ.Client.DependencyInjection;

namespace VibeMQ.Tests.Unit.Client;

public class ManagedVibeMQClientTests {
    [Fact]
    public void IsConnected_BeforeAnyCall_ReturnsFalse() {
        var factory = new FakeVibeMQClientFactory();
        var managed = new ManagedVibeMQClient(factory, NullLogger<ManagedVibeMQClient>.Instance);

        Assert.False(managed.IsConnected);
        Assert.Equal(0, factory.CreateCallCount);
    }

    [Fact]
    public void Dispose_WhenNeverConnected_DoesNotThrow() {
        var factory = new FakeVibeMQClientFactory();
        var managed = new ManagedVibeMQClient(factory, NullLogger<ManagedVibeMQClient>.Instance);

        managed.Dispose();

        Assert.Equal(0, factory.CreateCallCount);
    }

    [Fact]
    public void Dispose_WhenDisposed_DoesNotThrowOnSecondCall() {
        var factory = new FakeVibeMQClientFactory();
        var managed = new ManagedVibeMQClient(factory, NullLogger<ManagedVibeMQClient>.Instance);

        managed.Dispose();
        managed.Dispose();
    }

    [Fact]
    public async Task PublishAsync_AfterDispose_Throws() {
        var factory = new FakeVibeMQClientFactory();
        var managed = new ManagedVibeMQClient(factory, NullLogger<ManagedVibeMQClient>.Instance);
        managed.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => managed.PublishAsync("q", new { X = 1 })
        );
    }

    [Fact]
    public void IVibeMQClient_ResolvedFromDI_IsManagedVibeMQClient() {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVibeMQClient(settings => {
            settings.Host = "localhost";
            settings.Port = 2925;
        });
        var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IVibeMQClient>();

        Assert.IsType<ManagedVibeMQClient>(client);
    }

    /// <summary>
    /// Fake factory that never connects; used to test ManagedVibeMQClient without a broker.
    /// </summary>
    private sealed class FakeVibeMQClientFactory : IVibeMQClientFactory {
        public int CreateCallCount { get; private set; }

        public Task<VibeMQClient> CreateAsync(CancellationToken cancellationToken = default) {
            CreateCallCount++;
            throw new InvalidOperationException("Fake factory does not connect.");
        }
    }
}
