namespace VibeMQ.Enums;

/// <summary>
/// Available storage backend types for message persistence.
/// </summary>
public enum StorageType {
    /// <summary>
    /// In-memory storage. Fast but not durable — data is lost on restart.
    /// </summary>
    InMemory = 0,

    /// <summary>
    /// SQLite embedded database. Durable, zero-config, suitable for single-node deployments.
    /// </summary>
    Sqlite = 1,
}
