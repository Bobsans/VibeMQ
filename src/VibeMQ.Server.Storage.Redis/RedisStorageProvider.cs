using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using VibeMQ.Configuration;
using VibeMQ.Enums;
using VibeMQ.Interfaces;
using VibeMQ.Models;

namespace VibeMQ.Server.Storage.Redis;

/// <summary>
/// Redis-backed implementation of <see cref="IStorageProvider"/>.
/// Uses LIST for pending message order, HASH for message/queue/DLQ data.
/// </summary>
public sealed class RedisStorageProvider : IStorageProvider {
    private readonly RedisStorageOptions _options;
    private readonly RedisStorageConnectionFactory _connectionFactory;
    private readonly ILogger<RedisStorageProvider> _logger;
    private bool _initialized;

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    // One round-trip: get queue_name from hash, LREM from pending list, DEL message key.
    private const string RemoveMessageLua = @"
        local q = redis.call('HGET', KEYS[1], 'queue_name')
        if q == false or q == '' then return 0 end
        local pendingKey = ARGV[2] .. ':q:' .. tostring(q) .. ':pending'
        redis.call('LREM', pendingKey, 1, ARGV[1])
        return redis.call('DEL', KEYS[1])
    ";

    public RedisStorageProvider(
        RedisStorageOptions options,
        RedisStorageConnectionFactory connectionFactory,
        ILogger<RedisStorageProvider> logger) {
        _options = options;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    private IDatabase Db => _connectionFactory.GetConnection().GetDatabase(_options.Database);

    private string Q(string key) => $"{_options.KeyPrefix}:{key}";
    private string QueueMeta(string name) => Q($"q:{name}:meta");
    private string QueuePending(string name) => Q($"q:{name}:pending");
    private string MessageKey(string id) => Q($"m:{id}");
    private string DlqListKey() => Q("dlq");
    private string DlqMessageKey(string id) => Q($"dlq:m:{id}");
    private string QueuesSetKey() => Q("queues");

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken = default) {
        _initialized = true;
        _logger.LogInformation("Redis storage initialized (prefix: {Prefix}, database: {Database})",
            _options.KeyPrefix, _options.Database);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
        if (!_initialized) {
            return false;
        }

        try {
            var db = Db;
            await db.PingAsync().ConfigureAwait(false);
            return true;
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Redis availability check failed.");
            return false;
        }
    }

    // --- Messages ---

    /// <inheritdoc />
    public async Task SaveMessageAsync(BrokerMessage message, CancellationToken cancellationToken = default) {
        var db = Db;
        var msgKey = MessageKey(message.Id);
        var pendingKey = QueuePending(message.QueueName);

        var entries = new List<HashEntry> {
            new("id", message.Id),
            new("queue_name", message.QueueName),
            new("payload_json", SerializePayload(message.Payload)),
            new("timestamp", message.Timestamp.ToString("O")),
            new("headers_json", message.Headers.Count > 0 ? JsonSerializer.Serialize(message.Headers, JsonOptions) : ""),
            new("version", message.Version),
            new("priority", (int)message.Priority),
            new("delivery_attempts", message.DeliveryAttempts),
        };

        await db.HashSetAsync(msgKey, entries.ToArray()).ConfigureAwait(false);
        await db.ListRightPushAsync(pendingKey, message.Id).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveMessagesAsync(IReadOnlyList<BrokerMessage> messages, CancellationToken cancellationToken = default) {
        if (messages.Count == 0) {
            return;
        }

        var db = Db;
        var batch = db.CreateBatch();
        var tasks = new List<Task>(messages.Count * 2);
        foreach (var message in messages) {
            var msgKey = MessageKey(message.Id);
            var pendingKey = QueuePending(message.QueueName);
            var entries = new List<HashEntry> {
                new("id", message.Id),
                new("queue_name", message.QueueName),
                new("payload_json", SerializePayload(message.Payload)),
                new("timestamp", message.Timestamp.ToString("O")),
                new("headers_json", message.Headers.Count > 0 ? JsonSerializer.Serialize(message.Headers, JsonOptions) : ""),
                new("version", message.Version),
                new("priority", (int)message.Priority),
                new("delivery_attempts", message.DeliveryAttempts),
            };
            tasks.Add(batch.HashSetAsync(msgKey, entries.ToArray()));
            tasks.Add(batch.ListRightPushAsync(pendingKey, message.Id));
        }
        batch.Execute();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<BrokerMessage?> GetMessageAsync(string id, CancellationToken cancellationToken = default) {
        var db = Db;
        var msgKey = MessageKey(id);
        var entries = await db.HashGetAllAsync(msgKey).ConfigureAwait(false);
        if (entries.Length == 0) {
            return null;
        }
        return HashEntriesToBrokerMessage(entries);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveMessageAsync(string id, CancellationToken cancellationToken = default) {
        var db = Db;
        var msgKey = MessageKey(id);
        var result = await db.ScriptEvaluateAsync(
            RemoveMessageLua,
            new RedisKey[] { msgKey },
            new RedisValue[] { id, _options.KeyPrefix }).ConfigureAwait(false);
        return result.Resp2Type == ResultType.Integer && (long)result! == 1;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrokerMessage>> GetPendingMessagesAsync(string queueName, CancellationToken cancellationToken = default) {
        var db = Db;
        var pendingKey = QueuePending(queueName);
        var ids = await db.ListRangeAsync(pendingKey, 0, -1).ConfigureAwait(false);
        if (ids.Length == 0) {
            return Array.Empty<BrokerMessage>();
        }

        // Batch all HGETALL in one round-trip (2 RTT total instead of 1 + N).
        var batch = db.CreateBatch();
        var getTasks = new Task<HashEntry[]>[ids.Length];
        for (var i = 0; i < ids.Length; i++) {
            getTasks[i] = batch.HashGetAllAsync(MessageKey(ids[i]!));
        }
        batch.Execute();
        var result = new List<BrokerMessage>(ids.Length);
        foreach (var entries in await Task.WhenAll(getTasks).ConfigureAwait(false)) {
            if (entries.Length > 0) {
                result.Add(HashEntriesToBrokerMessage(entries));
            }
        }
        return result;
    }

    // --- Queues ---

    /// <inheritdoc />
    public async Task SaveQueueAsync(string name, QueueOptions options, CancellationToken cancellationToken = default) {
        var db = Db;
        var metaKey = QueueMeta(name);
        var exists = await db.KeyExistsAsync(metaKey).ConfigureAwait(false);

        await db.SetAddAsync(QueuesSetKey(), name).ConfigureAwait(false);
        if (exists) {
            await db.HashSetAsync(metaKey, "options_json", JsonSerializer.Serialize(options, JsonOptions)).ConfigureAwait(false);
        } else {
            await db.HashSetAsync(metaKey, new HashEntry[] {
                new("options_json", JsonSerializer.Serialize(options, JsonOptions)),
                new("created_at", DateTime.UtcNow.ToString("O")),
            }).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RemoveQueueAsync(string name, CancellationToken cancellationToken = default) {
        var db = Db;
        var pendingKey = QueuePending(name);
        var ids = await db.ListRangeAsync(pendingKey, 0, -1).ConfigureAwait(false);

        if (ids.Length > 0) {
            var batch = db.CreateBatch();
            var deleteTasks = new Task<bool>[ids.Length];
            for (var i = 0; i < ids.Length; i++) {
                deleteTasks[i] = batch.KeyDeleteAsync(MessageKey(ids[i]!));
            }
            batch.Execute();
            await Task.WhenAll(deleteTasks).ConfigureAwait(false);
        }

        await db.KeyDeleteAsync(new RedisKey[] { QueueMeta(name), pendingKey }).ConfigureAwait(false);
        await db.SetRemoveAsync(QueuesSetKey(), name).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredQueue>> GetAllQueuesAsync(CancellationToken cancellationToken = default) {
        var db = Db;
        var names = await db.SetMembersAsync(QueuesSetKey()).ConfigureAwait(false);
        var result = new List<StoredQueue>(names.Length);

        foreach (var name in names) {
            var metaKey = QueueMeta(name!);
            var optionsJson = await db.HashGetAsync(metaKey, "options_json").ConfigureAwait(false);
            var createdAtStr = await db.HashGetAsync(metaKey, "created_at").ConfigureAwait(false);
            if (optionsJson.IsNullOrEmpty || createdAtStr.IsNullOrEmpty) {
                continue;
            }
            var options = JsonSerializer.Deserialize<QueueOptions>(optionsJson!.ToString(), JsonOptions) ?? new QueueOptions();
            result.Add(new StoredQueue {
                Name = name!,
                Options = options,
                CreatedAt = DateTime.Parse(createdAtStr!, System.Globalization.CultureInfo.InvariantCulture),
            });
        }
        return result;
    }

    // --- Dead Letter Queue ---

    /// <inheritdoc />
    public async Task SaveDeadLetteredMessageAsync(DeadLetteredMessage message, CancellationToken cancellationToken = default) {
        var db = Db;
        var id = message.OriginalMessage.Id;
        var dlqKey = DlqMessageKey(id);
        await db.HashSetAsync(dlqKey, new HashEntry[] {
            new("message_json", JsonSerializer.Serialize(message.OriginalMessage, JsonOptions)),
            new("reason", (int)message.Reason),
            new("failed_at", message.FailedAt.ToString("O")),
        }).ConfigureAwait(false);
        await db.ListLeftPushAsync(DlqListKey(), id).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeadLetteredMessage>> GetDeadLetteredMessagesAsync(int count, CancellationToken cancellationToken = default) {
        if (count <= 0) {
            return Array.Empty<DeadLetteredMessage>();
        }
        var db = Db;
        // DLQ list: LPUSH adds newest at head, so tail is oldest. LRANGE -count -1 = oldest first.
        var ids = await db.ListRangeAsync(DlqListKey(), -count, -1).ConfigureAwait(false);
        var result = new List<DeadLetteredMessage>(ids.Length);

        foreach (var id in ids) {
            var entries = await db.HashGetAllAsync(DlqMessageKey(id!)).ConfigureAwait(false);
            if (entries.Length == 0) {
                continue;
            }
            var dict = entries.ToDictionary(x => x.Name.ToString(), x => x.Value);
            var messageJson = dict.GetValueOrDefault("message_json").ToString();
            var reason = (FailureReason)GetInt(dict, "reason", (int)FailureReason.MaxRetriesExceeded);
            var failedAtStr = dict.GetValueOrDefault("failed_at").ToString();
            if (string.IsNullOrEmpty(messageJson) || string.IsNullOrEmpty(failedAtStr)) {
                continue;
            }
            using var doc = JsonDocument.Parse(messageJson!);
            var originalMessage = JsonSerializer.Deserialize<BrokerMessage>(doc.RootElement, JsonOptions)!;
            result.Add(new DeadLetteredMessage {
                OriginalMessage = originalMessage,
                Reason = reason,
                FailedAt = DateTime.Parse(failedAtStr, System.Globalization.CultureInfo.InvariantCulture),
            });
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveDeadLetteredMessageAsync(string messageId, CancellationToken cancellationToken = default) {
        var db = Db;
        await db.ListRemoveAsync(DlqListKey(), messageId, 1).ConfigureAwait(false);
        return await db.KeyDeleteAsync(DlqMessageKey(messageId)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() {
        _initialized = false;
        _connectionFactory.DisposeConnection();
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    // --- Helpers ---

    private static string SerializePayload(JsonElement payload) {
        return payload.ValueKind != JsonValueKind.Undefined && payload.ValueKind != JsonValueKind.Null
            ? payload.GetRawText()
            : "";
    }

    private static BrokerMessage HashEntriesToBrokerMessage(HashEntry[] entries) {
        var dict = entries.ToDictionary(x => x.Name.ToString(), x => x.Value);
        var payloadJson = dict.GetValueOrDefault("payload_json").ToString();
        var headersJson = dict.GetValueOrDefault("headers_json").ToString();
        return new BrokerMessage {
            Id = dict.GetValueOrDefault("id").ToString()!,
            QueueName = dict.GetValueOrDefault("queue_name").ToString()!,
            Payload = !string.IsNullOrEmpty(payloadJson) ? JsonDocument.Parse(payloadJson).RootElement : default,
            Timestamp = DateTime.Parse(dict.GetValueOrDefault("timestamp").ToString()!, System.Globalization.CultureInfo.InvariantCulture),
            Headers = !string.IsNullOrEmpty(headersJson) ? JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson, JsonOptions) ?? new Dictionary<string, string>() : new Dictionary<string, string>(),
            Version = GetInt(dict, "version", 1),
            Priority = (MessagePriority)GetInt(dict, "priority", (int)MessagePriority.Normal),
            DeliveryAttempts = GetInt(dict, "delivery_attempts", 0),
        };
    }

    private static int GetInt(IReadOnlyDictionary<string, RedisValue> dict, string key, int defaultValue) {
        var v = dict.GetValueOrDefault(key);
        if (v.IsNullOrEmpty) {
            return defaultValue;
        }
        return int.TryParse(v.ToString(), out var n) ? n : defaultValue;
    }
}
