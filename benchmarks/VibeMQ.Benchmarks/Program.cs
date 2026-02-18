using BenchmarkDotNet.Running;
using VibeMQ.Benchmarks;

if (args.Length > 0 && (args[0] == "load" || args[0] == "load-test")) {
    var publishers = 4;
    var subscribers = 2;
    var duration = 10;
    var payloadSize = 128;
    for (var i = 1; i < args.Length; i++) {
        if (args[i] == "--publishers" && i + 1 < args.Length) { _ = int.TryParse(args[++i], out publishers); }
        else if (args[i] == "--subscribers" && i + 1 < args.Length) { _ = int.TryParse(args[++i], out subscribers); }
        else if (args[i] == "--duration" && i + 1 < args.Length) { _ = int.TryParse(args[++i], out duration); }
        else if (args[i] == "--payload-size" && i + 1 < args.Length) { _ = int.TryParse(args[++i], out payloadSize); }
    }
    await LoadTest.RunAsync(publisherCount: publishers, subscriberCount: subscribers, durationSeconds: duration, messagePayloadSize: payloadSize);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
