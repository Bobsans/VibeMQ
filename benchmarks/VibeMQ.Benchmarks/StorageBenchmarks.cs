using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using VibeMQ.Configuration;
using VibeMQ.Enums;
using VibeMQ.Interfaces;
using VibeMQ.Models;
using VibeMQ.Server.Storage;
using VibeMQ.Server.Storage.Redis;
using VibeMQ.Server.Storage.Sqlite;

namespace VibeMQ.Benchmarks;

/// <summary>
/// Benchmarks comparing InMemory, SQLite, and Redis storage providers.
/// Redis benchmarks require a running Redis instance at localhost:6379 (use Docker or local install).
/// Results are exported to BenchmarkDotNet.Artifacts/results/ (Markdown, CSV, JSON) for analysis.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("Storage")]
[MarkdownExporterAttribute.Default]
[MarkdownExporterAttribute.GitHub]
[CsvExporter]
[CsvMeasurementsExporter]
[JsonExporterAttribute.Full]
public class StorageBenchmarks {
    private IStorageProvider _storage = null!;
    private BrokerMessage[] _messages = null!;
    private string _queueName = null!;
    private string? _sqlitePath;

    [Params("InMemory", "Sqlite", "Redis")]
    public string StorageKind { get; set; } = "InMemory";

    [Params(100, 1000, 10_000)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup() {
        _queueName = $"bench-queue-{StorageKind}-{MessageCount}";
        _storage = CreateStorageProvider();
        _storage.InitializeAsync().GetAwaiter().GetResult();
        _storage.SaveQueueAsync(_queueName, new QueueOptions {
            Mode = DeliveryMode.RoundRobin,
            MaxQueueSize = MessageCount + 1000,
        }).GetAwaiter().GetResult();

        _messages = new BrokerMessage[MessageCount];
        for (var i = 0; i < MessageCount; i++) {
            _messages[i] = new BrokerMessage {
                Id = Guid.NewGuid().ToString("N"),
                QueueName = _queueName,
                Payload = JsonSerializer.SerializeToElement(new { Index = i }),
                Timestamp = DateTime.UtcNow,
            };
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup() {
        _storage.RemoveQueueAsync(_queueName).GetAwaiter().GetResult();
        _storage.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (_sqlitePath is not null && File.Exists(_sqlitePath)) {
            try {
                File.Delete(_sqlitePath);
            } catch {
                // Ignore cleanup errors
            }
        }
    }

    private IStorageProvider CreateStorageProvider() {
        return StorageKind switch {
            "InMemory" => new InMemoryStorageProvider(),
            "Sqlite" => CreateSqliteProvider(),
            "Redis" => CreateRedisProvider(),
            _ => throw new NotSupportedException($"Storage kind: {StorageKind}"),
        };
    }

    private IStorageProvider CreateSqliteProvider() {
        _sqlitePath = Path.Combine(Path.GetTempPath(), $"vibemq_bench_{Guid.NewGuid():N}.db");
        var options = new SqliteStorageOptions {
            DatabasePath = _sqlitePath,
            EnableWal = true,
            BusyTimeoutMs = 5000,
        };
        var logger = NullLogger<SqliteStorageProvider>.Instance;
        return new SqliteStorageProvider(options, logger);
    }

    private IStorageProvider CreateRedisProvider() {
        var options = new RedisStorageOptions {
            ConnectionString = "localhost:6379",
            KeyPrefix = "vibemq:bench",
            Database = 0
        };
        var logger = NullLogger<RedisStorageProvider>.Instance;
        var connectionFactoryLogger = NullLogger<RedisStorageConnectionFactory>.Instance;
        var connectionFactory = new RedisStorageConnectionFactory(options, connectionFactoryLogger);
        return new RedisStorageProvider(options, connectionFactory, logger);
    }

    /// <summary>Save N messages one-by-one (SaveMessageAsync per message).</summary>
    [Benchmark(Description = "SaveMessage x N")]
    public async Task SaveMessage_Sequential() {
        await _storage.RemoveQueueAsync(_queueName).ConfigureAwait(false);
        await _storage.SaveQueueAsync(_queueName, new QueueOptions {
            Mode = DeliveryMode.RoundRobin,
            MaxQueueSize = MessageCount + 1000,
        }).ConfigureAwait(false);

        for (var i = 0; i < MessageCount; i++) {
            await _storage.SaveMessageAsync(_messages[i]).ConfigureAwait(false);
        }
    }

    /// <summary>Save N messages in a single batch (SaveMessagesAsync).</summary>
    [Benchmark(Description = "SaveMessages batch")]
    public async Task SaveMessages_Batch() {
        await _storage.RemoveQueueAsync(_queueName).ConfigureAwait(false);
        await _storage.SaveQueueAsync(_queueName, new QueueOptions {
            Mode = DeliveryMode.RoundRobin,
            MaxQueueSize = MessageCount + 1000,
        }).ConfigureAwait(false);

        await _storage.SaveMessagesAsync(_messages).ConfigureAwait(false);
    }

    /// <summary>Load all pending messages for the queue (GetPendingMessagesAsync).</summary>
    [Benchmark(Description = "GetPendingMessages")]
    public async Task GetPendingMessages() {
        await _storage.RemoveQueueAsync(_queueName).ConfigureAwait(false);
        await _storage.SaveQueueAsync(_queueName, new QueueOptions {
            Mode = DeliveryMode.RoundRobin,
            MaxQueueSize = MessageCount + 1000,
        }).ConfigureAwait(false);
        await _storage.SaveMessagesAsync(_messages).ConfigureAwait(false);

        _ = await _storage.GetPendingMessagesAsync(_queueName).ConfigureAwait(false);
    }

    /// <summary>Round-trip: save N messages then load pending (SaveMessages + GetPending).</summary>
    [Benchmark(Description = "RoundTrip Save+GetPending")]
    public async Task RoundTrip_SaveThenGetPending() {
        await _storage.RemoveQueueAsync(_queueName).ConfigureAwait(false);
        await _storage.SaveQueueAsync(_queueName, new QueueOptions {
            Mode = DeliveryMode.RoundRobin,
            MaxQueueSize = MessageCount + 1000,
        }).ConfigureAwait(false);

        await _storage.SaveMessagesAsync(_messages).ConfigureAwait(false);
        _ = await _storage.GetPendingMessagesAsync(_queueName).ConfigureAwait(false);
    }

    /// <summary>Remove N messages one-by-one (RemoveMessageAsync after save).</summary>
    [Benchmark(Description = "RemoveMessage x N")]
    public async Task RemoveMessage_Sequential() {
        await _storage.RemoveQueueAsync(_queueName).ConfigureAwait(false);
        await _storage.SaveQueueAsync(_queueName, new QueueOptions {
            Mode = DeliveryMode.RoundRobin,
            MaxQueueSize = MessageCount + 1000,
        }).ConfigureAwait(false);
        await _storage.SaveMessagesAsync(_messages).ConfigureAwait(false);

        for (var i = 0; i < MessageCount; i++) {
            await _storage.RemoveMessageAsync(_messages[i].Id).ConfigureAwait(false);
        }
    }
}
