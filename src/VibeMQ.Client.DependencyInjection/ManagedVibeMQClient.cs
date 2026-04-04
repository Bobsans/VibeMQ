using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VibeMQ.Configuration;
using VibeMQ.Models;

namespace VibeMQ.Client.DependencyInjection;

/// <summary>
/// DI-managed VibeMQ client that connects lazily on first use and delegates to a single shared
/// <see cref="VibeMQClient"/> instance. Registered as <see cref="IVibeMQClient"/> Singleton when using
/// <see cref="ServiceCollectionExtensions.AddVibeMQClient"/>.
/// </summary>
sealed partial class ManagedVibeMQClient(IVibeMQClientFactory factory, ILogger<ManagedVibeMQClient>? logger = null) : IVibeMQClient, IDisposable {
    private const int DISPOSE_TIMEOUT_SECONDS = 5;

    private readonly IVibeMQClientFactory _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    private readonly ILogger<ManagedVibeMQClient> _logger = logger ?? NullLogger<ManagedVibeMQClient>.Instance;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private volatile VibeMQClient? _client;
    private volatile bool _disposed;

    /// <inheritdoc />
    public bool IsConnected => _client?.IsConnected ?? false;

    /// <inheritdoc />
    public async Task PublishAsync<T>(string queueName, T payload, CancellationToken cancellationToken = default) {
        var client = await EnsureClientAsync(cancellationToken).ConfigureAwait(false);
        await client.PublishAsync(queueName, payload, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(string queueName, T payload, Dictionary<string, string>? headers, CancellationToken cancellationToken = default) {
        var client = await EnsureClientAsync(cancellationToken).ConfigureAwait(false);
        await client.PublishAsync(queueName, payload, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable> SubscribeAsync<T>(string queueName, Func<T, Task> handler, CancellationToken cancellationToken = default) {
        var client = await EnsureClientAsync(cancellationToken).ConfigureAwait(false);
        return await client.SubscribeAsync(queueName, handler, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable> SubscribeAsync<TMessage, THandler>(string queueName, CancellationToken cancellationToken = default)
        where THandler : Interfaces.IMessageHandler<TMessage> {
        var client = await EnsureClientAsync(cancellationToken).ConfigureAwait(false);
        return await client.SubscribeAsync<TMessage, THandler>(queueName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CreateQueueAsync(string queueName, QueueOptions? options = null, CancellationToken cancellationToken = default) {
        var client = await EnsureClientAsync(cancellationToken).ConfigureAwait(false);
        await client.CreateQueueAsync(queueName, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken = default) {
        var client = await EnsureClientAsync(cancellationToken).ConfigureAwait(false);
        await client.DeleteQueueAsync(queueName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<QueueInfo?> GetQueueInfoAsync(string queueName, CancellationToken cancellationToken = default) {
        var client = await EnsureClientAsync(cancellationToken).ConfigureAwait(false);
        return await client.GetQueueInfoAsync(queueName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListQueuesAsync(CancellationToken cancellationToken = default) {
        var client = await EnsureClientAsync(cancellationToken).ConfigureAwait(false);
        return await client.ListQueuesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures the underlying client is created (lazy connect). Thread-safe; only one connection is created.
    /// </summary>
    private async Task<VibeMQClient> EnsureClientAsync(CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Fast path: client already initialized — no lock needed
        var existing = _client;
        if (existing is not null) {
            return existing;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_client is not null) {
                return _client;
            }
            try {
                _client = await _factory.CreateAsync(cancellationToken).ConfigureAwait(false);
            } catch {
                _client = null;
                throw;
            }
            return _client;
        } finally {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Disposes the managed client. Runs async dispose of the underlying client with a timeout to avoid blocking indefinitely.
    /// </summary>
    public void Dispose() {
        if (_disposed) {
            return;
        }
        _disposed = true;

        var client = Interlocked.Exchange(ref _client, null);
        if (client is null) {
            _initLock.Dispose();
            return;
        }

        try {
            var disposeTask = client.DisposeAsync().AsTask();
            if (!disposeTask.Wait(TimeSpan.FromSeconds(DISPOSE_TIMEOUT_SECONDS))) {
                LogDisposeTimeout(DISPOSE_TIMEOUT_SECONDS);
            }
        } catch (Exception ex) {
            LogDisposeError(ex);
        } finally {
            _initLock.Dispose();
        }
    }
}
