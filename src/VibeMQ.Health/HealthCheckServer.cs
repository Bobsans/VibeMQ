using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Core.Configuration;

namespace VibeMQ.Health;

/// <summary>
/// Lightweight HTTP health check server based on <see cref="HttpListener"/>.
/// No dependency on ASP.NET Core.
/// </summary>
public sealed partial class HealthCheckServer : IAsyncDisposable {
    private readonly HealthCheckOptions _options;
    private readonly ILogger<HealthCheckServer> _logger;
    private readonly Func<HealthStatus> _statusProvider;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    /// <summary>
    /// Creates a new health check server.
    /// </summary>
    /// <param name="options">Health check configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="statusProvider">Delegate that returns current broker health status.</param>
    public HealthCheckServer(
        HealthCheckOptions options,
        ILogger<HealthCheckServer> logger,
        Func<HealthStatus> statusProvider
    ) {
        _options = options;
        _logger = logger;
        _statusProvider = statusProvider;
    }

    /// <summary>
    /// Starts listening for health check HTTP requests.
    /// </summary>
    public void Start() {
        if (!_options.Enabled) {
            LogDisabled();
            return;
        }

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{_options.Port}/health/");
        _listener.Start();

        _cts = new CancellationTokenSource();
        _listenTask = AcceptLoopAsync(_cts.Token);

        LogStarted(_options.Port);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            try {
                var context = await _listener!.GetContextAsync().ConfigureAwait(false);
                _ = HandleRequestAsync(context);
            } catch (HttpListenerException) when (cancellationToken.IsCancellationRequested) {
                break;
            } catch (ObjectDisposedException) {
                break;
            } catch (Exception ex) {
                LogAcceptError(ex);
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context) {
        try {
            var status = _statusProvider();
            var isHealthy = status.IsHealthy;

            context.Response.StatusCode = isHealthy ? 200 : 503;
            context.Response.ContentType = "application/json";

            await JsonSerializer.SerializeAsync(
                context.Response.OutputStream,
                status
            ).ConfigureAwait(false);

            context.Response.Close();
        } catch (Exception ex) {
            LogHandleError(ex);

            try {
                context.Response.StatusCode = 500;
                context.Response.Close();
            } catch {
                // Ignore errors when closing the response
            }
        }
    }

    public async ValueTask DisposeAsync() {
        if (_cts is not null) {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        _listener?.Stop();
        _listener?.Close();

        if (_listenTask is not null) {
            try {
                await _listenTask.ConfigureAwait(false);
            } catch (OperationCanceledException) {
                // Expected on shutdown
            }
        }

        _cts?.Dispose();
    }
}

/// <summary>
/// Snapshot of the broker's health status, returned by the health endpoint.
/// </summary>
public sealed class HealthStatus {
    public required bool IsHealthy { get; init; }
    public required string Status { get; init; }
    public int ActiveConnections { get; init; }
    public int QueueCount { get; init; }
    public long MemoryUsageMb { get; init; } = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
