using System.Text.Json;
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
public sealed class RedisStorageProvider(
    RedisStorageOptions options,
    RedisStorageConnectionFactory connectionFactory,
    ILogger<RedisStorageProvider> logger
) : IStorageProvider, IInFlightMessageStorage, IBulkMessageStorage {
    private volatile bool _initialized;

    private static readonly JsonSerializerOptions _jsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // Atomic save: HSET message fields + RPUSH to a pending list in one round-trip.
    private const string SAVE_MESSAGE_LUA = @"
        redis.call('HSET', KEYS[1],
            'id', ARGV[1], 'queue_name', ARGV[2], 'payload_json', ARGV[3],
            'timestamp', ARGV[4], 'headers_json', ARGV[5], 'version', ARGV[6],
            'priority', ARGV[7], 'delivery_attempts', ARGV[8], 'state', 'pending')
        redis.call('HDEL', KEYS[1], 'client_id', 'next_retry_at', 'max_retry_attempts')
        redis.call('ZREM', KEYS[3], ARGV[1])
        redis.call('RPUSH', KEYS[2], ARGV[1])
        return 1
    ";

    // Atomic queue removal: delete all pending messages, meta key, pending list, and remove from set.
    private const string REMOVE_QUEUE_LUA = @"
        local ids = redis.call('LRANGE', KEYS[2], 0, -1)
        for i = 1, #ids do
            redis.call('DEL', ARGV[1] .. ':m:' .. ids[i])
        end
        local inflightIds = redis.call('ZRANGE', KEYS[4], 0, -1)
        for i = 1, #inflightIds do
            local id = inflightIds[i]
            local q = redis.call('HGET', ARGV[1] .. ':m:' .. id, 'queue_name')
            if q == ARGV[2] then
                redis.call('ZREM', KEYS[4], id)
                redis.call('DEL', ARGV[1] .. ':m:' .. id)
            end
        end
        redis.call('DEL', KEYS[1], KEYS[2])
        redis.call('SREM', KEYS[3], ARGV[2])
        return #ids
    ";

    // One round-trip: get queue_name from hash, LREM from a pending list, DEL message key.
    private const string REMOVE_MESSAGE_LUA = @"
        local q = redis.call('HGET', KEYS[1], 'queue_name')
        if q == false or q == '' then return 0 end
        local pendingKey = ARGV[2] .. ':q:' .. tostring(q) .. ':pending'
        redis.call('LREM', pendingKey, 1, ARGV[1])
        redis.call('ZREM', ARGV[2] .. ':inflight:due', ARGV[1])
        return redis.call('DEL', KEYS[1])
    ";

    private const string MARK_INFLIGHT_LUA = @"
        if redis.call('EXISTS', KEYS[1]) == 0 then return 0 end
        redis.call('LREM', KEYS[2], 1, ARGV[1])
        redis.call('ZADD', KEYS[3], ARGV[2], ARGV[1])
        redis.call('HSET', KEYS[1],
            'state', 'inflight',
            'client_id', ARGV[3],
            'max_retry_attempts', ARGV[4],
            'next_retry_at', ARGV[2],
            'delivery_attempts', ARGV[5])
        return 1
    ";

    private const string UPDATE_INFLIGHT_RETRY_LUA = @"
        if redis.call('EXISTS', KEYS[1]) == 0 then return 0 end
        redis.call('ZADD', KEYS[2], ARGV[2], ARGV[1])
        redis.call('HSET', KEYS[1],
            'state', 'inflight',
            'next_retry_at', ARGV[2],
            'delivery_attempts', ARGV[3])
        return 1
    ";

    private const string REQUEUE_INFLIGHT_LUA = @"
        local q = redis.call('HGET', KEYS[1], 'queue_name')
        if q == false or q == '' then return 0 end
        local pendingKey = ARGV[2] .. ':q:' .. tostring(q) .. ':pending'
        redis.call('ZREM', KEYS[2], ARGV[1])
        if redis.call('LPOS', pendingKey, ARGV[1]) == false then
            redis.call('RPUSH', pendingKey, ARGV[1])
        end
        redis.call('HSET', KEYS[1], 'state', 'pending')
        redis.call('HDEL', KEYS[1], 'client_id', 'next_retry_at', 'max_retry_attempts')
        return 1
    ";

    private const string REMOVE_INFLIGHT_STATE_LUA = @"
        if redis.call('EXISTS', KEYS[1]) == 0 then return 0 end
        redis.call('ZREM', KEYS[2], ARGV[1])
        redis.call('HSET', KEYS[1], 'state', 'pending')
        redis.call('HDEL', KEYS[1], 'client_id', 'next_retry_at', 'max_retry_attempts')
        return 1
    ";

    private const string RECOVER_INFLIGHT_LUA = @"
        local ids = redis.call('ZRANGE', KEYS[1], 0, -1)
        for i = 1, #ids do
            local id = ids[i]
            local q = redis.call('HGET', ARGV[1] .. ':m:' .. id, 'queue_name')
            if q ~= false and q ~= '' then
                local pendingKey = ARGV[1] .. ':q:' .. tostring(q) .. ':pending'
                if redis.call('LPOS', pendingKey, id) == false then
                    redis.call('RPUSH', pendingKey, id)
                end
                redis.call('HSET', ARGV[1] .. ':m:' .. id, 'state', 'pending')
                redis.call('HDEL', ARGV[1] .. ':m:' .. id, 'client_id', 'next_retry_at', 'max_retry_attempts')
            end
        end
        redis.call('DEL', KEYS[1])
        return ids
    ";

    private const string REMOVE_MESSAGES_BULK_LUA = @"
        local prefix = ARGV[1]
        for i = 2, #ARGV do
            local id = ARGV[i]
            local messageKey = prefix .. ':m:' .. id
            local q = redis.call('HGET', messageKey, 'queue_name')
            if q ~= false and q ~= '' then
                local pendingKey = prefix .. ':q:' .. tostring(q) .. ':pending'
                redis.call('LREM', pendingKey, 1, id)
            end
            redis.call('ZREM', KEYS[1], id)
            redis.call('DEL', messageKey)
        end
        return #ARGV - 1
    ";

    private IDatabase Db => connectionFactory.GetConnection().GetDatabase(options.Database);

    private string Q(string key) => $"{options.KeyPrefix}:{key}";
    private string QueueMeta(string name) => Q($"q:{name}:meta");
    private string QueuePending(string name) => Q($"q:{name}:pending");
    private string MessageKey(string id) => Q($"m:{id}");
    private string DlqListKey() => Q("dlq");
    private string DlqMessageKey(string id) => Q($"dlq:m:{id}");
    private string QueuesSetKey() => Q("queues");
    private string InFlightDueKey() => Q("inflight:due");

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken = default) {
        _initialized = true;
        logger.LogInformation("Redis storage initialized (prefix: {Prefix}, database: {Database})",
            options.KeyPrefix, options.Database);
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
            logger.LogWarning(ex, "Redis availability check failed.");
            return false;
        }
    }

    // --- Messages ---

    /// <inheritdoc />
    public async Task SaveMessageAsync(BrokerMessage message, CancellationToken cancellationToken = default) {
        var db = Db;
        var msgKey = MessageKey(message.Id);
        var pendingKey = QueuePending(message.QueueName);

        await db.ScriptEvaluateAsync(
            SAVE_MESSAGE_LUA,
            [msgKey, pendingKey, InFlightDueKey()],
            MessageToRedisValues(message)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveMessagesAsync(IReadOnlyList<BrokerMessage> messages, CancellationToken cancellationToken = default) {
        if (messages.Count == 0) {
            return;
        }

        var db = Db;
        var batch = db.CreateBatch();
        var tasks = new List<Task>(messages.Count);
        foreach (var message in messages) {
            var msgKey = MessageKey(message.Id);
            var pendingKey = QueuePending(message.QueueName);
            tasks.Add(batch.ScriptEvaluateAsync(
                SAVE_MESSAGE_LUA,
                [msgKey, pendingKey, InFlightDueKey()],
                MessageToRedisValues(message)));
        }

        batch.Execute();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<BrokerMessage?> GetMessageAsync(string id, CancellationToken cancellationToken = default) {
        var db = Db;
        var msgKey = MessageKey(id);
        var entries = await db.HashGetAllAsync(msgKey).ConfigureAwait(false);
        return entries.Length == 0 ? null : HashEntriesToBrokerMessage(entries);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveMessageAsync(string id, CancellationToken cancellationToken = default) {
        var db = Db;
        var msgKey = MessageKey(id);
        var result = await db.ScriptEvaluateAsync(
            REMOVE_MESSAGE_LUA,
            [msgKey],
            [id, options.KeyPrefix]).ConfigureAwait(false);
        return result.Resp2Type == ResultType.Integer && (long)result! == 1;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrokerMessage>> GetPendingMessagesAsync(string queueName, CancellationToken cancellationToken = default) {
        var db = Db;
        var pendingKey = QueuePending(queueName);
        var ids = await db.ListRangeAsync(pendingKey).ConfigureAwait(false);
        if (ids.Length == 0) {
            return [];
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
    public async Task SaveQueueAsync(string name, QueueOptions queueOptions, CancellationToken cancellationToken = default) {
        var db = Db;
        var metaKey = QueueMeta(name);
        var exists = await db.KeyExistsAsync(metaKey).ConfigureAwait(false);

        await db.SetAddAsync(QueuesSetKey(), name).ConfigureAwait(false);
        if (exists) {
            await db.HashSetAsync(metaKey, "options_json", JsonSerializer.Serialize(queueOptions, _jsonOptions)).ConfigureAwait(false);
        } else {
            await db.HashSetAsync(metaKey, [
                new HashEntry("options_json", JsonSerializer.Serialize(queueOptions, _jsonOptions)),
                new HashEntry("created_at", DateTime.UtcNow.ToString("O"))
            ]).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RemoveQueueAsync(string name, CancellationToken cancellationToken = default) {
        var db = Db;
        await db.ScriptEvaluateAsync(
            REMOVE_QUEUE_LUA,
            [QueueMeta(name), QueuePending(name), QueuesSetKey(), InFlightDueKey()],
            [options.KeyPrefix, name]).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredQueue>> GetAllQueuesAsync(CancellationToken cancellationToken = default) {
        var db = Db;
        var names = await db.SetMembersAsync(QueuesSetKey()).ConfigureAwait(false);
        if (names.Length == 0) {
            return [];
        }

        var batch = db.CreateBatch();
        var getTasks = new Task<HashEntry[]>[names.Length];
        for (var i = 0; i < names.Length; i++) {
            getTasks[i] = batch.HashGetAllAsync(QueueMeta(names[i]!));
        }

        batch.Execute();

        var allEntries = await Task.WhenAll(getTasks).ConfigureAwait(false);
        var result = new List<StoredQueue>(names.Length);
        for (var i = 0; i < names.Length; i++) {
            if (allEntries[i].Length == 0) {
                continue;
            }

            var dict = allEntries[i].ToDictionary(x => x.Name.ToString(), x => x.Value);
            var optionsJson = dict.GetValueOrDefault("options_json");
            var createdAtStr = dict.GetValueOrDefault("created_at");
            if (optionsJson.IsNullOrEmpty || createdAtStr.IsNullOrEmpty) {
                continue;
            }

            var queueOptions = JsonSerializer.Deserialize<QueueOptions>(optionsJson.ToString(), _jsonOptions) ?? new QueueOptions();
            result.Add(new StoredQueue {
                Name = names[i]!,
                Options = queueOptions,
                CreatedAt = DateTime.Parse(createdAtStr.ToString(), System.Globalization.CultureInfo.InvariantCulture)
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
        await db.HashSetAsync(dlqKey, [
            new HashEntry("message_json", JsonSerializer.Serialize(message.OriginalMessage, _jsonOptions)),
            new HashEntry("reason", (int)message.Reason),
            new HashEntry("failed_at", message.FailedAt.ToString("O"))
        ]).ConfigureAwait(false);
        await db.ListLeftPushAsync(DlqListKey(), id).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeadLetteredMessage>> GetDeadLetteredMessagesAsync(int count, CancellationToken cancellationToken = default) {
        if (count <= 0) {
            return [];
        }

        var db = Db;
        // DLQ list: LPUSH adds newest at head, so tail is oldest. LRANGE -count -1 = oldest first.
        var ids = await db.ListRangeAsync(DlqListKey(), -count).ConfigureAwait(false);
        if (ids.Length == 0) {
            return [];
        }

        var batch = db.CreateBatch();
        var getTasks = new Task<HashEntry[]>[ids.Length];
        for (var i = 0; i < ids.Length; i++) {
            getTasks[i] = batch.HashGetAllAsync(DlqMessageKey(ids[i]!));
        }

        batch.Execute();

        var allEntries = await Task.WhenAll(getTasks).ConfigureAwait(false);
        var result = new List<DeadLetteredMessage>(ids.Length);
        for (var i = 0; i < ids.Length; i++) {
            if (allEntries[i].Length == 0) {
                continue;
            }

            var dict = allEntries[i].ToDictionary(x => x.Name.ToString(), x => x.Value);
            var messageJson = dict.GetValueOrDefault("message_json").ToString();
            var reason = (FailureReason)GetInt(dict, "reason", (int)FailureReason.MaxRetriesExceeded);
            var failedAtStr = dict.GetValueOrDefault("failed_at").ToString();
            if (string.IsNullOrEmpty(messageJson) || string.IsNullOrEmpty(failedAtStr)) {
                continue;
            }

            var originalMessage = JsonSerializer.Deserialize<BrokerMessage>(messageJson, _jsonOptions)!;
            result.Add(new DeadLetteredMessage {
                OriginalMessage = originalMessage,
                Reason = reason,
                FailedAt = DateTime.Parse(failedAtStr, System.Globalization.CultureInfo.InvariantCulture)
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

    // --- In-flight delivery state ---

    /// <inheritdoc />
    public async Task MarkMessageInFlightAsync(
        BrokerMessage message,
        string clientId,
        int maxRetryAttempts,
        DateTime nextRetryAtUtc,
        CancellationToken cancellationToken = default
    ) {
        var db = Db;
        var messageKey = MessageKey(message.Id);
        var pendingKey = QueuePending(message.QueueName);
        var nextRetryScore = ToUnixTimeMilliseconds(nextRetryAtUtc);
        var attempts = message.DeliveryAttempts > 0 ? message.DeliveryAttempts : 1;

        await db.ScriptEvaluateAsync(
            MARK_INFLIGHT_LUA,
            [messageKey, pendingKey, InFlightDueKey()],
            [message.Id, nextRetryScore, clientId, maxRetryAttempts, attempts]
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateInFlightRetryAsync(
        string messageId,
        int deliveryAttempts,
        DateTime nextRetryAtUtc,
        CancellationToken cancellationToken = default
    ) {
        var db = Db;
        var messageKey = MessageKey(messageId);
        var nextRetryScore = ToUnixTimeMilliseconds(nextRetryAtUtc);
        await db.ScriptEvaluateAsync(
            UPDATE_INFLIGHT_RETRY_LUA,
            [messageKey, InFlightDueKey()],
            [messageId, nextRetryScore, deliveryAttempts]
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RequeueInFlightMessageAsync(string messageId, CancellationToken cancellationToken = default) {
        var db = Db;
        await db.ScriptEvaluateAsync(
            REQUEUE_INFLIGHT_LUA,
            [MessageKey(messageId), InFlightDueKey()],
            [messageId, options.KeyPrefix]
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveInFlightStateAsync(string messageId, CancellationToken cancellationToken = default) {
        var db = Db;
        await db.ScriptEvaluateAsync(
            REMOVE_INFLIGHT_STATE_LUA,
            [MessageKey(messageId), InFlightDueKey()],
            [messageId]
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrokerMessage>> RecoverInFlightMessagesAsync(CancellationToken cancellationToken = default) {
        var db = Db;
        var result = await db.ScriptEvaluateAsync(
            RECOVER_INFLIGHT_LUA,
            [InFlightDueKey()],
            [options.KeyPrefix]
        ).ConfigureAwait(false);

        if (result.Resp2Type != ResultType.Array || result.IsNull) {
            return [];
        }

        var redisResultArray = (RedisResult[])result!;
        if (redisResultArray.Length == 0) {
            return [];
        }

        var messageIds = redisResultArray
            .Select(x => (string?)x)
            .Where(x => !string.IsNullOrEmpty(x))
            .Cast<string>()
            .ToArray();

        if (messageIds.Length == 0) {
            return [];
        }

        var batch = db.CreateBatch();
        var getTasks = new Task<HashEntry[]>[messageIds.Length];
        for (var i = 0; i < messageIds.Length; i++) {
            getTasks[i] = batch.HashGetAllAsync(MessageKey(messageIds[i]));
        }

        batch.Execute();

        var allEntries = await Task.WhenAll(getTasks).ConfigureAwait(false);
        var recovered = new List<BrokerMessage>(allEntries.Length);
        foreach (var entries in allEntries) {
            if (entries.Length > 0) {
                recovered.Add(HashEntriesToBrokerMessage(entries));
            }
        }

        return recovered;
    }

    // --- Bulk operations ---

    /// <inheritdoc />
    public async Task RemoveMessagesAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default) {
        if (messageIds.Count == 0) {
            return;
        }

        var db = Db;
        var args = new RedisValue[messageIds.Count + 1];
        args[0] = options.KeyPrefix;
        for (var i = 0; i < messageIds.Count; i++) {
            args[i + 1] = messageIds[i];
        }

        await db.ScriptEvaluateAsync(
            REMOVE_MESSAGES_BULK_LUA,
            [InFlightDueKey()],
            args
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() {
        _initialized = false;
        connectionFactory.DisposeConnection();
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    // --- Helpers ---

    private static RedisValue[] MessageToRedisValues(BrokerMessage message) {
        return [
            message.Id,
            message.QueueName,
            SerializePayload(message.Payload),
            message.Timestamp.ToString("O"),
            message.Headers.Count > 0 ? JsonSerializer.Serialize(message.Headers, _jsonOptions) : "",
            message.Version,
            (int)message.Priority,
            message.DeliveryAttempts
        ];
    }

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
            Id = dict.GetValueOrDefault("id").ToString(),
            QueueName = dict.GetValueOrDefault("queue_name").ToString(),
            Payload = !string.IsNullOrEmpty(payloadJson) ? JsonSerializer.Deserialize<JsonElement>(payloadJson, _jsonOptions) : default,
            Timestamp = DateTime.Parse(dict.GetValueOrDefault("timestamp").ToString(), System.Globalization.CultureInfo.InvariantCulture),
            Headers = !string.IsNullOrEmpty(headersJson) ? JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson, _jsonOptions) ?? [] : [],
            Version = GetInt(dict, "version", 1),
            Priority = (MessagePriority)GetInt(dict, "priority", (int)MessagePriority.Normal),
            DeliveryAttempts = GetInt(dict, "delivery_attempts", 0)
        };
    }

    private static int GetInt(IReadOnlyDictionary<string, RedisValue> dict, string key, int defaultValue) {
        var v = dict.GetValueOrDefault(key);
        if (v.IsNullOrEmpty) {
            return defaultValue;
        }

        return int.TryParse(v.ToString(), out var n) ? n : defaultValue;
    }

    private static long ToUnixTimeMilliseconds(DateTime utcTimestamp) {
        var normalized = utcTimestamp.Kind == DateTimeKind.Utc
            ? utcTimestamp
            : utcTimestamp.ToUniversalTime();
        return new DateTimeOffset(normalized).ToUnixTimeMilliseconds();
    }
}
