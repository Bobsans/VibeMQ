using BenchmarkDotNet.Running;
using VibeMQ.Benchmarks;

if (args.Length > 0 && (args[0] == "load" || args[0] == "load-test")) {
    var publishers = 4;
    var subscribers = 2;
    var duration = 10;
    var payloadSize = 128;

    for (var i = 1; i < args.Length; i++) {
        _ = args[i] switch {
            "--publishers" when i + 1 < args.Length => int.TryParse(args[++i], out publishers),
            "--subscribers" when i + 1 < args.Length => int.TryParse(args[++i], out subscribers),
            "--duration" when i + 1 < args.Length => int.TryParse(args[++i], out duration),
            "--payload-size" when i + 1 < args.Length => int.TryParse(args[++i], out payloadSize),
            _ => false
        };
    }

    await LoadTest.RunAsync(publisherCount: publishers, subscriberCount: subscribers, durationSeconds: duration, messagePayloadSize: payloadSize);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
