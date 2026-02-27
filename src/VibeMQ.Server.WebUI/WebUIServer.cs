using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using VibeMQ.Health;
using VibeMQ.Metrics;
using VibeMQ.Models;

namespace VibeMQ.Server.WebUI;

/// <summary>
/// Lightweight HTTP server for the Web UI dashboard. Uses HttpListener; serves embedded SPA and /api/* endpoints.
/// </summary>
public sealed partial class WebUIServer : IAsyncDisposable {
    private static readonly JsonSerializerOptions _jsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly BrokerServer _broker;
    private readonly WebUIOptions _options;
    private readonly ILogger<WebUIServer> _logger;
    private readonly string _pathPrefix;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private ConcurrentDictionary<string, (byte[] Content, string ContentType)>? _staticCache;

    public WebUIServer(BrokerServer broker, WebUIOptions options, ILogger<WebUIServer> logger) {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pathPrefix = _options.PathPrefix.TrimEnd('/');
        if (!_pathPrefix.StartsWith('/')) {
            _pathPrefix = "/" + _pathPrefix;
        }
    }

    /// <summary>
    /// Starts the Web UI HTTP listener.
    /// </summary>
    public void Start() {
        if (!_options.Enabled) {
            LogDisabled();
            return;
        }

        _staticCache = LoadEmbeddedStatic();
        _listener = new HttpListener();
        // Use localhost to avoid HttpListener "Access Denied" on Windows when binding to +
        var prefix = $"http://localhost:{_options.Port}/";
        _listener.Prefixes.Add(prefix);
        _listener.Start();

        _cts = new CancellationTokenSource();
        _listenTask = AcceptLoopAsync(_cts.Token);

        LogStarted(_options.Port);
    }

    private static ConcurrentDictionary<string, (byte[] Content, string ContentType)> LoadEmbeddedStatic() {
        var cache = new ConcurrentDictionary<string, (byte[] Content, string ContentType)>(StringComparer.OrdinalIgnoreCase);
        var asm = Assembly.GetExecutingAssembly();
        var prefix = asm.GetName().Name + ".App.dist.";
        var names = asm.GetManifestResourceNames();

        foreach (var name in names) {
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) {
                continue;
            }

            var logicalPath = name
                .Replace(prefix, "", StringComparison.Ordinal);
            // Embedded name uses dots for path segments (e.g. "assets.index-xxx.js"). Convert first segment separator to slash.
            var firstDot = logicalPath.IndexOf('.', StringComparison.Ordinal);
            if (firstDot > 0 && firstDot < logicalPath.Length - 1 && logicalPath.IndexOf('.', firstDot + 1) >= 0) {
                logicalPath = string.Concat(logicalPath.AsSpan(0, firstDot), "/", logicalPath.AsSpan(firstDot + 1));
            }

            if (string.IsNullOrEmpty(logicalPath)) {
                continue;
            }

            string contentType = GetContentType(logicalPath);
            using var stream = asm.GetManifestResourceStream(name);
            if (stream is null) {
                continue;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            cache[logicalPath] = (ms.ToArray(), contentType);
        }

        return cache;
    }

    private static string GetContentType(string path) {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch {
            ".html" => "text/html; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".json" => "application/json",
            ".ico" => "image/x-icon",
            ".svg" => "image/svg+xml",
            ".woff2" => "font/woff2",
            ".woff" => "font/woff",
            _ => "application/octet-stream",
        };
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
            var path = context.Request.Url?.AbsolutePath ?? "/";
            path = path.TrimEnd('/');
            if (path.Length == 0) {
                path = "/";
            }

            // Strip path prefix if configured (e.g. /dashboard -> /)
            var localPath = path;
            if (_pathPrefix.Length > 1 && path.StartsWith(_pathPrefix, StringComparison.OrdinalIgnoreCase)) {
                localPath = path.Length == _pathPrefix.Length ? "/" : path.Substring(_pathPrefix.Length);
            }

            if (localPath.StartsWith("/api/", StringComparison.Ordinal)) {
                await HandleApiAsync(context, localPath).ConfigureAwait(false);
                return;
            }

            await ServeStaticAsync(context, localPath).ConfigureAwait(false);
        } catch (Exception ex) {
            LogHandleError(ex);
            try {
                context.Response.StatusCode = 500;
                context.Response.Close();
            } catch {
                // Ignore
            }
        }
    }

    private async Task HandleApiAsync(HttpListenerContext context, string localPath) {
        context.Response.ContentType = "application/json";

        switch (localPath) {
            case "/api/health":
                await HandleHealthAsync(context).ConfigureAwait(false);
                break;
            case "/api/version":
                await HandleVersionAsync(context).ConfigureAwait(false);
                break;
            case "/api/metrics":
                await HandleMetricsAsync(context).ConfigureAwait(false);
                break;
            case "/api/queues":
                await HandleQueuesListAsync(context).ConfigureAwait(false);
                break;
            default:
                if (localPath.StartsWith("/api/queues/", StringComparison.Ordinal)) {
                    var suffix = localPath.Substring("/api/queues/".Length).TrimEnd('/');
                    var parts = suffix.Split('/');
                    var queueName = parts.Length > 0 ? Uri.UnescapeDataString(parts[0]) : string.Empty;
                    var method = context.Request.HttpMethod;

                    if (parts.Length == 1) {
                        if (method == "GET") {
                            await HandleQueueInfoAsync(context, queueName).ConfigureAwait(false);
                        } else if (method == "DELETE") {
                            await HandleQueueDeleteAsync(context, queueName).ConfigureAwait(false);
                        } else {
                            context.Response.StatusCode = 405;
                            context.Response.Close();
                        }
                    } else if (parts.Length >= 2 && parts[1] == "messages") {
                        if (parts.Length == 2) {
                            if (method == "GET") {
                                await HandleQueueMessagesListAsync(context, queueName).ConfigureAwait(false);
                            } else if (method == "DELETE") {
                                await HandleQueuePurgeAsync(context, queueName).ConfigureAwait(false);
                            } else {
                                context.Response.StatusCode = 405;
                                context.Response.Close();
                            }
                        } else if (parts.Length == 3) {
                            var messageId = Uri.UnescapeDataString(parts[2]);
                            if (method == "GET") {
                                await HandleQueueMessageGetAsync(context, queueName, messageId).ConfigureAwait(false);
                            } else if (method == "DELETE") {
                                await HandleQueueMessageDeleteAsync(context, queueName, messageId).ConfigureAwait(false);
                            } else {
                                context.Response.StatusCode = 405;
                                context.Response.Close();
                            }
                        } else {
                            context.Response.StatusCode = 404;
                            context.Response.Close();
                        }
                    } else {
                        context.Response.StatusCode = 404;
                        context.Response.Close();
                    }
                } else {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
                break;
        }
    }

    private static async Task HandleVersionAsync(HttpListenerContext context) {
        var serverVersion = GetAssemblyVersion(typeof(BrokerServer));
        var webUiVersion = GetAssemblyVersion(typeof(WebUIServer));
        var payload = new { server_version = serverVersion, webui_version = webUiVersion };

        context.Response.StatusCode = 200;
        await JsonSerializer.SerializeAsync(context.Response.OutputStream, payload, _jsonOptions).ConfigureAwait(false);
        context.Response.Close();
    }

    private static string GetAssemblyVersion(Type type) {
        var asm = type.Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (!string.IsNullOrEmpty(info?.InformationalVersion)) {
            var ver = info.InformationalVersion;
            // Strip git hash suffix (e.g. "1.0.0+5346ccf4..." -> "1.0.0") for display
            var plus = ver.IndexOf('+');
            return plus >= 0 ? ver[..plus] : ver;
        }
        var name = asm.GetName();
        var v = name.Version;
        if (v is null) return "0.0.0";
        var build = v.Build >= 0 ? v.Build : 0;
        return $"{v.Major}.{v.Minor}.{build}";
    }

    private async Task HandleHealthAsync(HttpListenerContext context) {
        var snapshot = _broker.Metrics.GetSnapshot();
        var status = new HealthStatus {
            IsHealthy = true,
            Status = "ok",
            ActiveConnections = _broker.ActiveConnections,
            QueueCount = _broker.QueueCount,
            InFlightMessages = _broker.InFlightMessages,
            TotalMessagesPublished = snapshot.TotalMessagesPublished,
            TotalMessagesDelivered = snapshot.TotalMessagesDelivered,
            MemoryUsageMb = (int)(snapshot.MemoryUsageBytes / 1024 / 1024),
            Timestamp = DateTime.UtcNow,
        };

        context.Response.StatusCode = 200;
        await JsonSerializer.SerializeAsync(context.Response.OutputStream, status, _jsonOptions).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task HandleMetricsAsync(HttpListenerContext context) {
        var snapshot = _broker.Metrics.GetSnapshot();
        context.Response.StatusCode = 200;
        await JsonSerializer.SerializeAsync(context.Response.OutputStream, snapshot, _jsonOptions).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task HandleQueuesListAsync(HttpListenerContext context) {
        var names = await _broker.ListQueuesAsync(CancellationToken.None).ConfigureAwait(false);
        context.Response.StatusCode = 200;
        await JsonSerializer.SerializeAsync(context.Response.OutputStream, names, _jsonOptions).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task HandleQueueInfoAsync(HttpListenerContext context, string name) {
        if (string.IsNullOrEmpty(name)) {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        var info = await _broker.GetQueueInfoAsync(name, CancellationToken.None).ConfigureAwait(false);
        if (info is null) {
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }

        context.Response.StatusCode = 200;
        await JsonSerializer.SerializeAsync(context.Response.OutputStream, info, _jsonOptions).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task HandleQueueMessagesListAsync(HttpListenerContext context, string queueName) {
        if (string.IsNullOrEmpty(queueName)) {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        var limit = 50;
        var offset = 0;
        var query = context.Request.Url?.Query;
        if (!string.IsNullOrEmpty(query) && query.StartsWith('?')) {
            foreach (var pair in query[1..].Split('&')) {
                var kv = pair.Split('=', 2, StringSplitOptions.None);
                if (kv.Length != 2) continue;
                var key = Uri.UnescapeDataString(kv[0].Trim());
                var value = Uri.UnescapeDataString(kv[1].Trim());
                if (key == "limit" && int.TryParse(value, out var l)) limit = Math.Clamp(l, 1, 100);
                if (key == "offset" && int.TryParse(value, out var o)) offset = Math.Max(0, o);
            }
        }

        var messages = await _broker.GetPendingMessagesForDashboardAsync(queueName, limit, offset, CancellationToken.None).ConfigureAwait(false);
        context.Response.StatusCode = 200;
        await JsonSerializer.SerializeAsync(context.Response.OutputStream, messages, _jsonOptions).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task HandleQueueMessageGetAsync(HttpListenerContext context, string queueName, string messageId) {
        if (string.IsNullOrEmpty(queueName) || string.IsNullOrEmpty(messageId)) {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        var message = await _broker.GetMessageForDashboardAsync(queueName, messageId, CancellationToken.None).ConfigureAwait(false);
        if (message is null) {
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }

        context.Response.StatusCode = 200;
        await JsonSerializer.SerializeAsync(context.Response.OutputStream, message, _jsonOptions).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task HandleQueueMessageDeleteAsync(HttpListenerContext context, string queueName, string messageId) {
        if (string.IsNullOrEmpty(queueName) || string.IsNullOrEmpty(messageId)) {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        var removed = await _broker.RemoveMessageFromQueueAsync(queueName, messageId, CancellationToken.None).ConfigureAwait(false);
        context.Response.StatusCode = removed ? 204 : 404;
        context.Response.Close();
    }

    private async Task HandleQueuePurgeAsync(HttpListenerContext context, string queueName) {
        if (string.IsNullOrEmpty(queueName)) {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        var ok = await _broker.PurgeQueueAsync(queueName, CancellationToken.None).ConfigureAwait(false);
        context.Response.StatusCode = ok ? 204 : 404;
        context.Response.Close();
    }

    private async Task HandleQueueDeleteAsync(HttpListenerContext context, string queueName) {
        if (string.IsNullOrEmpty(queueName)) {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        await _broker.DeleteQueueAsync(queueName, CancellationToken.None).ConfigureAwait(false);
        context.Response.StatusCode = 204;
        context.Response.Close();
    }

    private async Task ServeStaticAsync(HttpListenerContext context, string localPath) {
#pragma warning disable CA1836 // ConcurrentDictionary has no IsEmpty; Count == 0 is correct
        if (_staticCache is null || _staticCache.Count == 0) {
#pragma warning restore CA1836
            context.Response.StatusCode = 503;
            context.Response.ContentType = "text/plain";
            await context.Response.OutputStream.WriteAsync("Web UI assets not embedded. Run 'npm run build' in App/ and rebuild the project."u8.ToArray()).ConfigureAwait(false);
            context.Response.Close();
            return;
        }

        var key = localPath.TrimStart('/');
        if (string.IsNullOrEmpty(key)) {
            key = "index.html";
        }

        if (_staticCache.TryGetValue(key, out var entry)) {
            context.Response.StatusCode = 200;
            context.Response.ContentType = entry.ContentType;
            context.Response.ContentLength64 = entry.Content.Length;
            await context.Response.OutputStream.WriteAsync(entry.Content).ConfigureAwait(false);
            context.Response.Close();
            return;
        }

        // SPA fallback: any non-file request gets index.html
        if (!key.Contains('.') && _staticCache.TryGetValue("index.html", out entry)) {
            context.Response.StatusCode = 200;
            context.Response.ContentType = entry.ContentType;
            context.Response.ContentLength64 = entry.Content.Length;
            await context.Response.OutputStream.WriteAsync(entry.Content).ConfigureAwait(false);
            context.Response.Close();
            return;
        }

        context.Response.StatusCode = 404;
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
                // Expected
            }
        }

        _cts?.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Web UI disabled.")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Web UI dashboard listening on port {Port}. Open http://localhost:{Port}/ in a browser.")]
    private partial void LogStarted(int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Web UI accept loop error.")]
    private partial void LogAcceptError(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Web UI request handling error.")]
    private partial void LogHandleError(Exception ex);
}
