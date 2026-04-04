using Microsoft.Extensions.Logging.Abstractions;

namespace VibeMQ.Server.WebUI;

/// <summary>
/// Extension methods for running the broker with the Web UI dashboard.
/// </summary>
public static class RunWithWebUIExtensions {
    /// <param name="broker">The broker server instance.</param>
    extension(BrokerServer broker) {
        /// <summary>
        /// Runs the broker and the Web UI server concurrently. Use the default Web UI port (12925) and options.
        /// Shutdown is coordinated: when the given cancellation token is canceled, both broker and Web UI stop.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to stop both broker and Web UI.</param>
        /// <returns>A task that completes when both have stopped.</returns>
        public Task RunWithWebUIAsync(CancellationToken cancellationToken = default) {
            return broker.RunWithWebUIAsync(new WebUIOptions(), cancellationToken);
        }

        /// <summary>
        /// Runs the broker and the Web UI server concurrently with the given options.
        /// Shutdown is coordinated: when the given cancellation token is canceled, both broker and Web UI stop.
        /// </summary>
        /// <param name="webUIOptions">Web UI configuration (port, enabled, path prefix).</param>
        /// <param name="cancellationToken">Cancellation token to stop both broker and Web UI.</param>
        /// <returns>A task that completes when both have stopped.</returns>
        public async Task RunWithWebUIAsync(WebUIOptions webUIOptions, CancellationToken cancellationToken = default) {
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
}
