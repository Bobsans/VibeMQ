using VibeMQ.Enums;
using VibeMQ.Server.Auth.Models;

namespace VibeMQ.Server.Auth;

/// <summary>
/// Data-access layer for the auth SQLite database (users and permissions).
/// </summary>
public interface IAuthRepository {
    /// <summary>Creates the database schema if it does not exist.</summary>
    Task CreateSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the user record, or null if not found.</summary>
    Task<UserRecord?> FindUserAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>Creates a new user.</summary>
    Task CreateUserAsync(UserRecord user, CancellationToken cancellationToken = default);

    /// <summary>Updates the BCrypt hash for an existing user.</summary>
    Task UpdatePasswordHashAsync(string username, string hash, CancellationToken cancellationToken = default);

    /// <summary>Deletes a user and all their permissions (cascade).</summary>
    Task DeleteUserAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>Returns all users (without password hashes).</summary>
    Task<IReadOnlyList<UserRecord>> ListUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all permission entries for the user.</summary>
    Task<IReadOnlyList<PermissionEntry>> GetPermissionsAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or replaces the permission entry for (username, queuePattern).
    /// </summary>
    Task GrantPermissionAsync(string username, string queuePattern, QueueOperation[] operations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the permission entry for (username, queuePattern).
    /// No-op if the entry does not exist.
    /// </summary>
    Task RevokePermissionAsync(string username, string queuePattern, CancellationToken cancellationToken = default);
}
