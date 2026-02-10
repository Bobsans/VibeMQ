using System.Text.Json;
using BenchmarkDotNet.Attributes;
using VibeMQ.Protocol;
using VibeMQ.Protocol.Framing;

namespace VibeMQ.Benchmarks;

/// <summary>
/// Benchmarks for protocol framing (read/write) operations.
/// Measures the impact of ArrayPool optimizations.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class FramingBenchmarks {
    private ProtocolMessage _smallMessage = null!;
    private ProtocolMessage _largeMessage = null!;
    private MemoryStream _stream = null!;

    [GlobalSetup]
    public void Setup() {
        _smallMessage = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = "test-queue",
        };

        _largeMessage = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = "test-queue",
            Payload = JsonSerializer.SerializeToElement(new {
                Name = "benchmark-test",
                Values = Enumerable.Range(0, 100).Select(i => new { Index = i, Data = new string('x', 100) }).ToArray(),
            }),
            Headers = Enumerable.Range(0, 10).ToDictionary(i => $"header-{i}", i => $"value-{i}"),
        };
    }

    [IterationSetup]
    public void IterationSetup() {
        _stream = new MemoryStream(capacity: 65536);
    }

    [IterationCleanup]
    public void IterationCleanup() {
        _stream.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task WriteSmallMessage() {
        _stream.Position = 0;
        await FrameWriter.WriteFrameAsync(_stream, _smallMessage);
    }

    [Benchmark]
    public async Task WriteLargeMessage() {
        _stream.Position = 0;
        await FrameWriter.WriteFrameAsync(_stream, _largeMessage);
    }

    [Benchmark]
    public async Task WriteAndReadSmallMessage() {
        _stream.Position = 0;
        await FrameWriter.WriteFrameAsync(_stream, _smallMessage);
        _stream.Position = 0;
        await FrameReader.ReadFrameAsync(_stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE);
    }

    [Benchmark]
    public async Task WriteAndReadLargeMessage() {
        _stream.Position = 0;
        await FrameWriter.WriteFrameAsync(_stream, _largeMessage);
        _stream.Position = 0;
        await FrameReader.ReadFrameAsync(_stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE);
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    public async Task Write_N_SmallMessages(int count) {
        _stream.Position = 0;

        for (var i = 0; i < count; i++) {
            await FrameWriter.WriteFrameAsync(_stream, _smallMessage);
        }
    }
}
