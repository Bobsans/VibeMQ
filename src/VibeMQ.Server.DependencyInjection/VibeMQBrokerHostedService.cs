using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VibeMQ.Server.DependencyInjection;

/// <summary>
/// Hosted service that runs the VibeMQ broker and integrates with the generic host lifecycle.
/// </summary>
sealed partial class VibeMQBrokerHostedService(BrokerServer broker, ILogger<VibeMQBrokerHostedService> logger) : IHostedService {
    private readonly ILogger<VibeMQBrokerHostedService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task StartAsync(CancellationToken cancellationToken) {
        _ = RunBrokerAsync(cancellationToken);
        return Task.CompletedTask;
    }

    private async Task RunBrokerAsync(CancellationToken cancellationToken) {
        try {
            await broker.RunAsync(cancellationToken).ConfigureAwait(false);
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
        await broker.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
