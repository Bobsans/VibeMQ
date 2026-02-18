using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VibeMQ.Client.DependencyInjection;

internal sealed class VibeMQClientFactory : IVibeMQClientFactory {
    private readonly IOptions<VibeMQClientSettings> _options;
    private readonly ILogger<VibeMQClient> _logger;

    public VibeMQClientFactory(IOptions<VibeMQClientSettings> options, ILogger<VibeMQClient> logger) {
        _options = options;
        _logger = logger;
    }

    public Task<VibeMQClient> CreateAsync(CancellationToken cancellationToken = default) {
        var settings = _options.Value;
        return VibeMQClient.ConnectAsync(
            settings.Host,
            settings.Port,
            settings.ClientOptions,
            _logger,
            cancellationToken
        );
    }
}
