namespace VibeMQ.Server.Storage.Redis;

/// <summary>
/// Configuration options for the Redis storage provider.
/// </summary>
public sealed class RedisStorageOptions {
    /// <summary>
    /// Redis connection string (e.g. "localhost:6379", "redis.example.com:6379,password=secret").
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Redis database number. Default: 0.
    /// </summary>
    public int Database { get; set; }

    /// <summary>
    /// Key prefix for all VibeMQ keys to avoid collisions. Default: "vibemq".
    /// </summary>
    public string KeyPrefix { get; set; } = "vibemq";

    /// <summary>
    /// Connection timeout in milliseconds. Default: 5000.
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Sync timeout in milliseconds for individual operations. Default: 5000.
    /// </summary>
    public int SyncTimeoutMs { get; set; } = 5000;
}
