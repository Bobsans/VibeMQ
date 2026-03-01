# VibeMQ Benchmarks (Linux)

Publish of `VibeMQ.Benchmarks` for **linux-x64**: single-file executable, **framework-dependent** (requires .NET 8 on the server). Native libs (e.g. SQLite) are emitted alongside the executable.

## Build (on Windows)

From repo root:

```powershell
dotnet publish benchmarks/VibeMQ.Benchmarks/VibeMQ.Benchmarks.csproj -c Release -f net8.0 -r linux-x64 --self-contained false -p:PublishSingleFile=true -o benchmarks/publish-linux
```

Copy the entire `benchmarks/publish-linux` folder to your Linux server. The server must have **.NET 8 runtime** installed. Keep `libe_sqlite3.so` and the `amd64` folder next to `VibeMQ.Benchmarks` (required for SQLite).

For **ARM64** (e.g. Raspberry Pi, Graviton):

```powershell
dotnet publish benchmarks/VibeMQ.Benchmarks/VibeMQ.Benchmarks.csproj -c Release -f net8.0 -r linux-arm64 --self-contained false -p:PublishSingleFile=true -o benchmarks/publish-linux-arm64
```

## Requirements on server

- **Redis** (optional): required only for Redis storage benchmarks. Run on `localhost:6379`.

## Run

```bash
chmod +x run-storage-benchmark.sh
./run-storage-benchmark.sh
```

This runs all **StorageBenchmarks** (InMemory, Sqlite, Redis) without interactive prompt. Full run can take 25–35 minutes.

### Run only one storage type

```bash
./run-storage-benchmark.sh -- InMemory   # fastest, no deps
./run-storage-benchmark.sh -- Sqlite     # no Redis needed
./run-storage-benchmark.sh -- Redis      # needs Redis on localhost:6379
```

### Run without the script

```bash
./VibeMQ.Benchmarks --filter "*StorageBenchmarks*"
./VibeMQ.Benchmarks --filter "*StorageBenchmarks*InMemory*"
```

### Other benchmarks

```bash
./VibeMQ.Benchmarks --filter "*QueueBenchmarks*"
./VibeMQ.Benchmarks   # lists all and asks to select
```

## Run with full output for analysis

To capture everything for later analysis (e.g. by another tool or human):

1. **Console output** — save the full log:
   ```bash
   ./run-storage-benchmark.sh storage-benchmark-console.txt
   ```
   (First argument ending in `.txt` or `.log` is the log file; output is also printed to stdout.)
   Or with manual tee:
   ```bash
   ./run-storage-benchmark.sh 2>&1 | tee storage-benchmark-console.txt
   ```

2. **Exported files** — BenchmarkDotNet writes results into `BenchmarkDotNet.Artifacts/results/` in the current directory:
   - `*StorageBenchmarks*-report-default.md` — summary table (Markdown)
   - `*StorageBenchmarks*-report-github.md` — GitHub-style table
   - `*StorageBenchmarks*-report-full.json` — full stats (Mean, StdDev, N, percentiles, etc.)
   - `*StorageBenchmarks*.csv` — CSV summary
   - `*StorageBenchmarks*-measurements.csv` — per-iteration measurements

After the run, share the **console log** and/or the **Markdown** and **JSON** from `BenchmarkDotNet.Artifacts/results/` for analysis.
