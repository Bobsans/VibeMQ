using System.Text.Json;
using Microsoft.Data.Sqlite;
using VibeMQ.Enums;
using VibeMQ.Server.Auth.Models;

namespace VibeMQ.Server.Auth;

/// <summary>
/// SQLite-backed implementation of <see cref="IAuthRepository"/>.
/// Uses a dedicated <c>auth.db</c> file separate from the message storage provider.
/// </summary>
public sealed class SqliteAuthRepository(string databasePath) : IAuthRepository {
    private readonly string _connectionString = new SqliteConnectionStringBuilder {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared
    }.ToString();

    private const string SCHEMA = """
        PRAGMA journal_mode = WAL;
        PRAGMA foreign_keys = ON;

        CREATE TABLE IF NOT EXISTS users (
            username      TEXT    PRIMARY KEY,
            password_hash TEXT    NOT NULL,
            is_superuser  INTEGER NOT NULL DEFAULT 0,
            created_at    INTEGER NOT NULL,
            updated_at    INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS permissions (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            username      TEXT    NOT NULL REFERENCES users(username) ON DELETE CASCADE,
            queue_pattern TEXT    NOT NULL,
            operations    TEXT    NOT NULL,
            created_at    INTEGER NOT NULL,
            UNIQUE(username, queue_pattern)
        );

        CREATE INDEX IF NOT EXISTS ix_permissions_username ON permissions(username);
        """;

    /// <inheritdoc />
    public async Task CreateSchemaAsync(CancellationToken cancellationToken = default) {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SCHEMA;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UserRecord?> FindUserAsync(string username, CancellationToken cancellationToken = default) {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
        SELECT username, password_hash, is_superuser, created_at, updated_at
        FROM users WHERE username = $username;
        """;
        command.Parameters.AddWithValue("$username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            return null;
        }

        return ReadUserRecord(reader);
    }

    /// <inheritdoc />
    public async Task CreateUserAsync(UserRecord user, CancellationToken cancellationToken = default) {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
        INSERT INTO users (username, password_hash, is_superuser, created_at, updated_at)
        VALUES ($username, $password_hash, $is_superuser, $created_at, $updated_at);
        """;
        command.Parameters.AddWithValue("$username", user.Username);
        command.Parameters.AddWithValue("$password_hash", user.PasswordHash);
        command.Parameters.AddWithValue("$is_superuser", user.IsSuperuser ? 1 : 0);
        command.Parameters.AddWithValue("$created_at", user.CreatedAt);
        command.Parameters.AddWithValue("$updated_at", user.UpdatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdatePasswordHashAsync(string username, string hash, CancellationToken cancellationToken = default) {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
        UPDATE users SET password_hash = $hash, updated_at = $updated_at
        WHERE username = $username;
        """;
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$username", username);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(string username, CancellationToken cancellationToken = default) {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM users WHERE username = $username;";
        command.Parameters.AddWithValue("$username", username);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserRecord>> ListUsersAsync(CancellationToken cancellationToken = default) {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT username, password_hash, is_superuser, created_at, updated_at
            FROM users ORDER BY username;
        """;

        var result = new List<UserRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            result.Add(ReadUserRecord(reader));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PermissionEntry>> GetPermissionsAsync(string username, CancellationToken cancellationToken = default) {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT queue_pattern, operations FROM permissions WHERE username = $username;";
        command.Parameters.AddWithValue("$username", username);

        var result = new List<PermissionEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            var pattern = reader.GetString(0);
            var opsJson = reader.GetString(1);
            var ops = JsonSerializer.Deserialize<QueueOperation[]>(opsJson) ?? [];
            result.Add(new PermissionEntry(pattern, ops));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task GrantPermissionAsync(string username, string queuePattern, QueueOperation[] operations, CancellationToken cancellationToken = default) {
        var opsJson = JsonSerializer.Serialize(operations);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO permissions (username, queue_pattern, operations, created_at)
            VALUES ($username, $queue_pattern, $operations, $created_at)
            ON CONFLICT(username, queue_pattern) DO UPDATE SET operations = excluded.operations;
        """;
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$queue_pattern", queuePattern);
        command.Parameters.AddWithValue("$operations", opsJson);
        command.Parameters.AddWithValue("$created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RevokePermissionAsync(string username, string queuePattern, CancellationToken cancellationToken = default) {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM permissions WHERE username = $username AND queue_pattern = $queue_pattern;";
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$queue_pattern", queuePattern);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken) {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static UserRecord ReadUserRecord(SqliteDataReader reader) {
        return new UserRecord {
            Username = reader.GetString(0),
            PasswordHash = reader.GetString(1),
            IsSuperuser = reader.GetInt64(2) != 0,
            CreatedAt = reader.GetInt64(3),
            UpdatedAt = reader.GetInt64(4)
        };
    }
}
