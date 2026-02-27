using Microsoft.Extensions.Logging.Abstractions;

namespace VibeMQ.Server.WebUI;

/// <summary>
/// Extension methods for running the broker with the Web UI dashboard.
/// </summary>
public static class RunWithWebUIExtensions {
    /// <summary>
    /// Runs the broker and the Web UI server concurrently. Use default Web UI port (12925) and options.
    /// Shutdown is coordinated: when the given cancellation token is cancelled, both broker and Web UI stop.
    /// </summary>
    /// <param name="broker">The broker server instance.</param>
    /// <param name="cancellationToken">Cancellation token to stop both broker and Web UI.</param>
    /// <returns>A task that completes when both have stopped.</returns>
    public static Task RunWithWebUIAsync(this BrokerServer broker, CancellationToken cancellationToken = default) {
        return RunWithWebUIAsync(broker, new WebUIOptions(), cancellationToken);
    }

    /// <summary>
    /// Runs the broker and the Web UI server concurrently with the given options.
    /// Shutdown is coordinated: when the given cancellation token is cancelled, both broker and Web UI stop.
    /// </summary>
    /// <param name="broker">The broker server instance.</param>
    /// <param name="webUIOptions">Web UI configuration (port, enabled, path prefix).</param>
    /// <param name="cancellationToken">Cancellation token to stop both broker and Web UI.</param>
    /// <returns>A task that completes when both have stopped.</returns>
    public static async Task RunWithWebUIAsync(
        this BrokerServer broker,
        WebUIOptions webUIOptions,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(broker);
        ArgumentNullException.ThrowIfNull(webUIOptions);

        var logger = NullLogger<WebUIServer>.Instance;
        var webUi = new WebUIServer(broker, webUIOptions, logger);
        webUi.Start();

        try {
            await broker.RunAsync(cancellationToken).ConfigureAwait(false);
        } finally {
            await webUi.DisposeAsync().ConfigureAwait(false);
        }
    }
}
