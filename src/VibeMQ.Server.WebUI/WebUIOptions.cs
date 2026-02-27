namespace VibeMQ.Server.WebUI;

/// <summary>
/// Configuration for the Web UI dashboard HTTP server.
/// </summary>
public sealed class WebUIOptions {
    /// <summary>
    /// Whether the Web UI server is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// HTTP port for the dashboard. Default: 12925.
    /// </summary>
    public int Port { get; set; } = 12925;

    /// <summary>
    /// URL path prefix for the dashboard (e.g. "/" or "/dashboard/").
    /// Must start and end with "/". Default: "/".
    /// </summary>
    public string PathPrefix { get; set; } = "/";
}
