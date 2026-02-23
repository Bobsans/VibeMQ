using VibeMQ.Models;

namespace VibeMQ.Interfaces;

/// <summary>
/// Optional operational interface for storage maintenance.
/// Not all providers support these operations (e.g. Redis may not support backup).
/// <para>
/// Check at runtime: <c>if (storageProvider is IStorageManagement management) { ... }</c>
/// </para>
/// </summary>
public interface IStorageManagement {
    /// <summary>
    /// Creates a backup of the storage to the specified path.
    /// </summary>
    Task BackupAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores storage from a backup at the specified path.
    /// </summary>
    Task RestoreAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compacts the storage to reclaim space (e.g. SQLite VACUUM, RocksDB CompactRange).
    /// </summary>
    Task CompactAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns current storage statistics.
    /// </summary>
    Task<StorageStats> GetStatsAsync(CancellationToken cancellationToken = default);
}
