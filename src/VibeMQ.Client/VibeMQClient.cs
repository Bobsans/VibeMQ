using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace VibeMQ.Client;

/// <summary>
/// Client for connecting to a VibeMQ message broker.
/// Handles connection, publish/subscribe, reconnection, and graceful disconnect.
/// </summary>
public sealed partial class VibeMQClient : IAsyncDisposable {
    private readonly ClientOptions _options;
    private readonly ILogger<VibeMQClient> _logger;

    private VibeMQClient(ClientOptions options, ILogger<VibeMQClient>? logger) {
        _options = options;
        _logger = logger ?? NullLogger<VibeMQClient>.Instance;
    }

    /// <summary>
    /// Connects to a VibeMQ broker and returns an initialized client.
    /// </summary>
    public static Task<VibeMQClient> ConnectAsync(
        string host,
        int port,
        ClientOptions? options = null,
        ILogger<VibeMQClient>? logger = null,
        CancellationToken cancellationToken = default
    ) {
        var opts = options ?? new ClientOptions();
        var client = new VibeMQClient(opts, logger);

        client.LogConnecting(host, port);

        // TODO: Establish TCP connection, authenticate, start read loop
        throw new NotImplementedException("VibeMQClient.ConnectAsync is not yet implemented.");
    }

    /// <summary>
    /// Publishes a message to the specified queue.
    /// </summary>
    public Task PublishAsync<T>(string queueName, T payload, CancellationToken cancellationToken = default) {
        // TODO: Serialize payload, create protocol message, send via frame writer
        throw new NotImplementedException("VibeMQClient.PublishAsync is not yet implemented.");
    }

    /// <summary>
    /// Subscribes to a queue and invokes the handler for each received message.
    /// </summary>
    public Task<IAsyncDisposable> SubscribeAsync<T>(
        string queueName,
        Func<T, Task> handler,
        CancellationToken cancellationToken = default
    ) {
        // TODO: Send subscribe command, register handler, return disposable subscription
        throw new NotImplementedException("VibeMQClient.SubscribeAsync is not yet implemented.");
    }

    /// <summary>
    /// Gracefully disconnects from the broker.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default) {
        LogDisconnecting();

        // TODO: Send disconnect command, close TCP connection
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() {
        await DisconnectAsync().ConfigureAwait(false);
    }
}
