using System.Text.Json;
using BenchmarkDotNet.Attributes;
using VibeMQ.Core.Configuration;
using VibeMQ.Core.Enums;
using VibeMQ.Core.Models;
using VibeMQ.Server.Queues;

namespace VibeMQ.Benchmarks;

/// <summary>
/// Benchmarks for in-memory queue operations.
/// </summary>
[MemoryDiagnoser]
public class QueueBenchmarks {
    private MessageQueue _queue = null!;
    private BrokerMessage[] _messages = null!;

    [Params(1000, 10_000)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void Setup() {
        _queue = new MessageQueue("bench-queue", new QueueOptions {
            Mode = DeliveryMode.RoundRobin,
            MaxQueueSize = MessageCount + 1000,
        });

        _messages = new BrokerMessage[MessageCount];

        for (var i = 0; i < MessageCount; i++) {
            _messages[i] = new BrokerMessage {
                Id = i.ToString(),
                QueueName = "bench-queue",
                Payload = JsonSerializer.SerializeToElement(new { Index = i }),
            };
        }
    }

    [IterationSetup]
    public void IterationSetup() {
        // Clear the queue for each iteration
        while (_queue.Dequeue() is not null) { }
    }

    [Benchmark]
    public void Enqueue_N_Messages() {
        for (var i = 0; i < MessageCount; i++) {
            _queue.Enqueue(_messages[i]);
        }
    }

    [Benchmark]
    public void Enqueue_Then_Dequeue_N_Messages() {
        for (var i = 0; i < MessageCount; i++) {
            _queue.Enqueue(_messages[i]);
        }

        for (var i = 0; i < MessageCount; i++) {
            _queue.Dequeue();
        }
    }

    [Benchmark]
    public void RoundRobin_Index_N() {
        for (var i = 0; i < MessageCount; i++) {
            _queue.GetNextRoundRobinIndex(5);
        }
    }
}
