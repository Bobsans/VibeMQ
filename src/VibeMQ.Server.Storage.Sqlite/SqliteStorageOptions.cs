namespace VibeMQ.Server.Storage.Sqlite;

/// <summary>
/// Configuration options for the SQLite storage provider.
/// </summary>
public sealed class SqliteStorageOptions {
    /// <summary>
    /// Path to the SQLite database file. Default: "vibemq.db".
    /// </summary>
    public string DatabasePath { get; set; } = "vibemq.db";

    /// <summary>
    /// Enables WAL (Write-Ahead Logging) journal mode for better concurrent read performance.
    /// Default: true.
    /// </summary>
    public bool EnableWal { get; set; } = true;

    /// <summary>
    /// Timeout in milliseconds when waiting for a database lock. Default: 5000.
    /// </summary>
    public int BusyTimeoutMs { get; set; } = 5000;
}
