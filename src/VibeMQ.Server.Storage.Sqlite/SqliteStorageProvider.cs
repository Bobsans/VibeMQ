using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using VibeMQ.Configuration;
using VibeMQ.Enums;
using VibeMQ.Interfaces;
using VibeMQ.Models;

namespace VibeMQ.Server.Storage.Sqlite;

/// <summary>
/// SQLite-backed implementation of <see cref="IStorageProvider"/> and <see cref="IStorageManagement"/>.
/// Uses WAL mode for concurrent read performance and parameterized queries for safety.
/// </summary>
public sealed partial class SqliteStorageProvider : IStorageProvider, IStorageManagement {
    private readonly SqliteStorageOptions _options;
    private readonly ILogger<SqliteStorageProvider> _logger;
    private readonly string _connectionString;
    private volatile bool _initialized;

    private static readonly JsonSerializerOptions _jsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public SqliteStorageProvider(SqliteStorageOptions options, ILogger<SqliteStorageProvider> logger) {
        _options = options;
        _logger = logger;
        _connectionString = new SqliteConnectionStringBuilder {
            DataSource = options.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = (options.BusyTimeoutMs + 999) / 1000,
        }.ToString();
    }

    // --- Lifecycle ---

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // Enable WAL mode if configured
        if (_options.EnableWal) {
            await ExecuteNonQueryAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken).ConfigureAwait(false);
        }

        // Create schema
        await ExecuteNonQueryAsync(connection, SCHEMA, cancellationToken).ConfigureAwait(false);

        _initialized = true;
        LogInitialized(_options.DatabasePath, _options.EnableWal);
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
        if (!_initialized) {
            return false;
        }

        try {
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return true;
        } catch {
            return false;
        }
    }

    // --- Messages ---

    /// <inheritdoc />
    public async Task SaveMessageAsync(BrokerMessage message, CancellationToken cancellationToken = default) {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = """
        INSERT OR REPLACE INTO messages (id, queue_name, payload_json, timestamp, headers_json, version, priority, delivery_attempts)
        VALUES ($id, $queue_name, $payload_json, $timestamp, $headers_json, $version, $priority, $delivery_attempts);
        """;

        AddMessageParameters(command, message);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveMessagesAsync(IReadOnlyList<BrokerMessage> messages, CancellationToken cancellationToken = default) {
        if (messages.Count == 0) {
            return;
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
            INSERT OR REPLACE INTO messages (id, queue_name, payload_json, timestamp, headers_json, version, priority, delivery_attempts)
            VALUES ($id, $queue_name, $payload_json, $timestamp, $headers_json, $version, $priority, $delivery_attempts);
            """;

            var pId = command.Parameters.Add("$id", SqliteType.Text);
            var pQueue = command.Parameters.Add("$queue_name", SqliteType.Text);
            var pPayload = command.Parameters.Add("$payload_json", SqliteType.Text);
            var pTimestamp = command.Parameters.Add("$timestamp", SqliteType.Text);
            var pHeaders = command.Parameters.Add("$headers_json", SqliteType.Text);
            var pVersion = command.Parameters.Add("$version", SqliteType.Integer);
            var pPriority = command.Parameters.Add("$priority", SqliteType.Integer);
            var pDelivery = command.Parameters.Add("$delivery_attempts", SqliteType.Integer);

            foreach (var message in messages) {
                pId.Value = message.Id;
                pQueue.Value = message.QueueName;
                pPayload.Value = SerializePayload(message.Payload);
                pTimestamp.Value = message.Timestamp.ToString("O");
                pHeaders.Value = message.Headers.Count > 0
                    ? JsonSerializer.Serialize(message.Headers, _jsonOptions)
                    : DBNull.Value;
                pVersion.Value = message.Version;
                pPriority.Value = (int)message.Priority;
                pDelivery.Value = message.DeliveryAttempts;

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            LogBatchSaved(messages.Count);
        } catch {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<BrokerMessage?> GetMessageAsync(string id, CancellationToken cancellationToken = default) {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT id, queue_name, payload_json, timestamp, headers_json, version, priority, delivery_attempts FROM messages WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadBrokerMessage(reader)
            : null;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveMessageAsync(string id, CancellationToken cancellationToken = default) {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = "DELETE FROM messages WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrokerMessage>> GetPendingMessagesAsync(string queueName, CancellationToken cancellationToken = default) {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = """
        SELECT id, queue_name, payload_json, timestamp, headers_json, version, priority, delivery_attempts
        FROM messages
        WHERE queue_name = $queue_name
        ORDER BY timestamp ASC;
        """;
        command.Parameters.AddWithValue("$queue_name", queueName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var messages = new List<BrokerMessage>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            messages.Add(ReadBrokerMessage(reader));
        }

        return messages;
    }

    // --- Queues ---

    /// <inheritdoc />
    public async Task SaveQueueAsync(string name, QueueOptions options, CancellationToken cancellationToken = default) {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = """
        INSERT INTO queues (name, options_json, created_at) VALUES ($name, $options_json, $created_at)
        ON CONFLICT(name) DO UPDATE SET options_json = $options_json;
        """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$options_json", JsonSerializer.Serialize(options, _jsonOptions));
        command.Parameters.AddWithValue("$created_at", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveQueueAsync(string name, CancellationToken cancellationToken = default) {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM queues WHERE name = $name;";
        command.Parameters.AddWithValue("$name", name);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredQueue>> GetAllQueuesAsync(CancellationToken cancellationToken = default) {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT name, options_json, created_at FROM queues;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var queues = new List<StoredQueue>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            var optionsJson = reader.GetString(1);
            var options = JsonSerializer.Deserialize<QueueOptions>(optionsJson, _jsonOptions) ?? new QueueOptions();

            queues.Add(new StoredQueue {
                Name = reader.GetString(0),
                Options = options,
                CreatedAt = DateTime.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture),
            });
        }

        return queues;
    }

    // --- Dead Letter Queue ---

    /// <inheritdoc />
    public async Task SaveDeadLetteredMessageAsync(DeadLetteredMessage message, CancellationToken cancellationToken = default) {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = """
        INSERT INTO dead_letters (message_id, message_json, reason, failed_at)
        VALUES ($message_id, $message_json, $reason, $failed_at);
        """;
        command.Parameters.AddWithValue("$message_id", message.OriginalMessage.Id);
        command.Parameters.AddWithValue("$message_json", JsonSerializer.Serialize(message.OriginalMessage, _jsonOptions));
        command.Parameters.AddWithValue("$reason", (int)message.Reason);
        command.Parameters.AddWithValue("$failed_at", message.FailedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeadLetteredMessage>> GetDeadLetteredMessagesAsync(int count, CancellationToken cancellationToken = default) {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT message_id, message_json, reason, failed_at
            FROM dead_letters
            ORDER BY failed_at ASC
            LIMIT $count;
        """;
        command.Parameters.AddWithValue("$count", count);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var messages = new List<DeadLetteredMessage>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            var messageJson = reader.GetString(1);
            var originalMessage = JsonSerializer.Deserialize<BrokerMessage>(messageJson, _jsonOptions)!;

            messages.Add(new DeadLetteredMessage {
                OriginalMessage = originalMessage,
                Reason = (FailureReason)reader.GetInt32(2),
                FailedAt = DateTime.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture),
            });
        }

        return messages;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveDeadLetteredMessageAsync(string messageId, CancellationToken cancellationToken = default) {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        command.CommandText = "DELETE FROM dead_letters WHERE message_id = $message_id;";
        command.Parameters.AddWithValue("$message_id", messageId);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return rows > 0;
    }

    // --- IStorageManagement ---

    /// <inheritdoc />
    public async Task BackupAsync(string path, CancellationToken cancellationToken = default) {
        await using var source = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());

        await destination.OpenAsync(cancellationToken).ConfigureAwait(false);
        source.BackupDatabase(destination);
        LogBackupCreated(path);
    }

    /// <inheritdoc />
    public async Task RestoreAsync(string path, CancellationToken cancellationToken = default) {
        await using var source = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString());

        await source.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var destination = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        source.BackupDatabase(destination);
        LogBackupRestored(path);
    }

    /// <inheritdoc />
    public async Task CompactAsync(CancellationToken cancellationToken = default) {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteNonQueryAsync(connection, "VACUUM;", cancellationToken).ConfigureAwait(false);
        LogCompacted();
    }

    /// <inheritdoc />
    public async Task<StorageStats> GetStatsAsync(CancellationToken cancellationToken = default) {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // Single query for all table counts (3 RTT → 1)
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM messages),
                (SELECT COUNT(*) FROM queues),
                (SELECT COUNT(*) FROM dead_letters);
        """;
        await using var reader = await countCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var totalMessages = reader.GetInt64(0);
        var totalQueues = reader.GetInt64(1);
        var totalDeadLettered = reader.GetInt64(2);

        var pageCount = await ExecuteScalarAsync<long>(connection, "PRAGMA page_count;", cancellationToken).ConfigureAwait(false);
        var pageSize = await ExecuteScalarAsync<long>(connection, "PRAGMA page_size;", cancellationToken).ConfigureAwait(false);

        return new StorageStats {
            TotalMessages = totalMessages,
            TotalQueues = totalQueues,
            TotalDeadLettered = totalDeadLettered,
            StorageSizeBytes = pageCount * pageSize,
        };
    }

    // --- Dispose ---

    /// <inheritdoc />
    public ValueTask DisposeAsync() {
        _initialized = false;
        return ValueTask.CompletedTask;
    }

    // --- Helpers ---

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken) {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Foreign keys must be enabled per-connection in SQLite
        await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken).ConfigureAwait(false);

        return connection;
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken) {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ExecuteScalarAsync<T>(SqliteConnection connection, string sql, CancellationToken cancellationToken) {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return (T)Convert.ChangeType(result!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddMessageParameters(SqliteCommand command, BrokerMessage message) {
        command.Parameters.AddWithValue("$id", message.Id);
        command.Parameters.AddWithValue("$queue_name", message.QueueName);
        command.Parameters.AddWithValue("$payload_json", SerializePayload(message.Payload));
        command.Parameters.AddWithValue("$timestamp", message.Timestamp.ToString("O"));
        command.Parameters.AddWithValue("$headers_json",
            message.Headers.Count > 0
                ? JsonSerializer.Serialize(message.Headers, _jsonOptions)
                : DBNull.Value);
        command.Parameters.AddWithValue("$version", message.Version);
        command.Parameters.AddWithValue("$priority", (int)message.Priority);
        command.Parameters.AddWithValue("$delivery_attempts", message.DeliveryAttempts);
    }

    private static object SerializePayload(JsonElement payload) {
        return payload.ValueKind != JsonValueKind.Undefined
            ? payload.GetRawText()
            : DBNull.Value;
    }

    private static BrokerMessage ReadBrokerMessage(SqliteDataReader reader) {
        var payloadJson = reader.IsDBNull(2) ? null : reader.GetString(2);
        var headersJson = reader.IsDBNull(4) ? null : reader.GetString(4);

        return new BrokerMessage {
            Id = reader.GetString(0),
            QueueName = reader.GetString(1),
            Payload = payloadJson is not null
                ? JsonSerializer.Deserialize<JsonElement>(payloadJson, _jsonOptions)
                : default,
            Timestamp = DateTime.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture),
            Headers = headersJson is not null
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson, _jsonOptions) ?? new()
                : new(),
            Version = reader.GetInt32(5),
            Priority = (MessagePriority)reader.GetInt32(6),
            DeliveryAttempts = reader.GetInt32(7),
        };
    }

    // --- Schema ---

    private const string SCHEMA = """
        CREATE TABLE IF NOT EXISTS queues (
            name            TEXT PRIMARY KEY,
            options_json    TEXT NOT NULL,
            created_at      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
        );

        CREATE TABLE IF NOT EXISTS messages (
            id                  TEXT PRIMARY KEY,
            queue_name          TEXT NOT NULL,
            payload_json        TEXT,
            timestamp           TEXT NOT NULL,
            headers_json        TEXT,
            version             INTEGER NOT NULL DEFAULT 1,
            priority            INTEGER NOT NULL DEFAULT 1,
            delivery_attempts   INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY (queue_name) REFERENCES queues(name) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS ix_messages_queue_timestamp ON messages(queue_name, timestamp);
        CREATE INDEX IF NOT EXISTS ix_messages_queue_priority  ON messages(queue_name, priority DESC, timestamp);

        CREATE TABLE IF NOT EXISTS dead_letters (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            message_id      TEXT NOT NULL,
            message_json    TEXT NOT NULL,
            reason          INTEGER NOT NULL,
            failed_at       TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_dead_letters_failed_at ON dead_letters(failed_at);
        """;

    // --- Logging ---

    [LoggerMessage(Level = LogLevel.Information, Message = "SQLite storage initialized at {path} (WAL: {walEnabled}).")]
    private partial void LogInitialized(string path, bool walEnabled);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Batch saved {count} messages.")]
    private partial void LogBatchSaved(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backup created at {path}.")]
    private partial void LogBackupCreated(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backup restored from {path}.")]
    private partial void LogBackupRestored(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Storage compacted (VACUUM).")]
    private partial void LogCompacted();
}
