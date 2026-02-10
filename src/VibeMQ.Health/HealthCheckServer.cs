using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using VibeMQ.Core.Configuration;
using VibeMQ.Core.Metrics;

namespace VibeMQ.Health;

/// <summary>
/// Lightweight HTTP health check server based on <see cref="HttpListener"/>.
/// Exposes /health/ and /metrics/ endpoints. No dependency on ASP.NET Core.
/// </summary>
public sealed partial class HealthCheckServer : IAsyncDisposable {
    private static readonly JsonSerializerOptions _jsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly HealthCheckOptions _options;
    private readonly ILogger<HealthCheckServer> _logger;
    private readonly Func<HealthStatus> _statusProvider;
    private readonly Func<MetricsSnapshot>? _metricsProvider;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    /// <summary>
    /// Creates a new health check server.
    /// </summary>
    /// <param name="options">Health check configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="statusProvider">Delegate that returns current broker health status.</param>
    /// <param name="metricsProvider">Optional delegate that returns a metrics snapshot.</param>
    public HealthCheckServer(
        HealthCheckOptions options,
        ILogger<HealthCheckServer> logger,
        Func<HealthStatus> statusProvider,
        Func<MetricsSnapshot>? metricsProvider = null
    ) {
        _options = options;
        _logger = logger;
        _statusProvider = statusProvider;
        _metricsProvider = metricsProvider;
    }

    /// <summary>
    /// Starts listening for HTTP requests.
    /// </summary>
    public void Start() {
        if (!_options.Enabled) {
            LogDisabled();
            return;
        }

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://+:{_options.Port}/health/");
        _listener.Prefixes.Add($"http://+:{_options.Port}/metrics/");
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
            var path = context.Request.Url?.AbsolutePath?.TrimEnd('/') ?? string.Empty;

            switch (path) {
                case "/health":
                    await HandleHealthAsync(context).ConfigureAwait(false);
                    break;

                case "/metrics":
                    await HandleMetricsAsync(context).ConfigureAwait(false);
                    break;

                default:
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    break;
            }
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

    private async Task HandleHealthAsync(HttpListenerContext context) {
        var status = _statusProvider();

        context.Response.StatusCode = status.IsHealthy ? 200 : 503;
        context.Response.ContentType = "application/json";

        await JsonSerializer.SerializeAsync(
            context.Response.OutputStream, status, _jsonOptions
        ).ConfigureAwait(false);

        context.Response.Close();
    }

    private async Task HandleMetricsAsync(HttpListenerContext context) {
        if (_metricsProvider is null) {
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }

        var snapshot = _metricsProvider();

        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";

        await JsonSerializer.SerializeAsync(
            context.Response.OutputStream, snapshot, _jsonOptions
        ).ConfigureAwait(false);

        context.Response.Close();
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
