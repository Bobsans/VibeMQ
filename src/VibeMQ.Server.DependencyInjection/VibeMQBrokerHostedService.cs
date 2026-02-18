using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VibeMQ.Server.DependencyInjection;

/// <summary>
/// Hosted service that runs the VibeMQ broker and integrates with the generic host lifecycle.
/// </summary>
internal sealed partial class VibeMQBrokerHostedService : IHostedService {
    private readonly BrokerServer _broker;
    private readonly ILogger<VibeMQBrokerHostedService> _logger;

    public VibeMQBrokerHostedService(BrokerServer broker, ILogger<VibeMQBrokerHostedService> logger) {
        _broker = broker;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        _ = RunBrokerAsync(cancellationToken);
        return Task.CompletedTask;
    }

    private async Task RunBrokerAsync(CancellationToken cancellationToken) {
        try {
            await _broker.RunAsync(cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) {
            // Expected on shutdown
        } catch (Exception ex) {
            LogBrokerFaulted(ex);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "VibeMQ broker faulted.")]
    private partial void LogBrokerFaulted(Exception ex);

    public async Task StopAsync(CancellationToken cancellationToken) {
        await _broker.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
