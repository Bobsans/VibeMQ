using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeMQ.Core.Configuration;

namespace VibeMQ.Server;

/// <summary>
/// Main entry point for the VibeMQ message broker.
/// Manages the server lifecycle: start, accept connections, graceful shutdown.
/// </summary>
public sealed partial class BrokerServer : IAsyncDisposable {
    private readonly BrokerOptions _options;
    private readonly ILogger<BrokerServer> _logger;
    private readonly CancellationTokenSource _shutdownCts = new();

    public BrokerServer(IOptions<BrokerOptions> options, ILogger<BrokerServer> logger) {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Starts the broker and blocks until shutdown is requested.
    /// </summary>
    public Task RunAsync(CancellationToken cancellationToken = default) {
        LogServerStarting(_options.Port);

        // TODO: Implement TCP listener, connection accept loop, message routing
        throw new NotImplementedException("BrokerServer.RunAsync is not yet implemented.");
    }

    /// <summary>
    /// Initiates graceful shutdown of the broker.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default) {
        LogServerShuttingDown();

        await _shutdownCts.CancelAsync().ConfigureAwait(false);

        // TODO: Notify clients, wait for in-flight messages, close connections
    }

    public async ValueTask DisposeAsync() {
        await StopAsync().ConfigureAwait(false);
        _shutdownCts.Dispose();
    }
}
