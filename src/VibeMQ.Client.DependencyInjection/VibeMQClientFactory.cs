using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VibeMQ.Client.DependencyInjection;

sealed class VibeMQClientFactory(
    IOptions<VibeMQClientSettings> options,
    ILogger<VibeMQClient> logger,
    IServiceProvider? serviceProvider = null
) : IVibeMQClientFactory {
    public Task<VibeMQClient> CreateAsync(CancellationToken cancellationToken = default) {
        var settings = options.Value;
        return VibeMQClient.ConnectAsync(
            settings.Host,
            settings.Port,
            settings.ClientOptions,
            logger,
            serviceProvider,
            cancellationToken
        );
    }
}
