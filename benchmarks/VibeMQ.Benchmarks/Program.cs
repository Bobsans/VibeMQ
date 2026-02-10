using BenchmarkDotNet.Running;
using VibeMQ.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
