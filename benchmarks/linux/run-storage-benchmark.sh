#!/usr/bin/env bash
# Run StorageBenchmarks (InMemory, Sqlite, Redis) on Linux.
# Usage:
#   ./run-storage-benchmark.sh              # all storage benchmarks
#   ./run-storage-benchmark.sh -- InMemory  # InMemory only
#   ./run-storage-benchmark.sh log.txt      # save full console output to log.txt (and stdout)
# For Redis benchmarks, ensure Redis is running on localhost:6379.
# Results are also written to BenchmarkDotNet.Artifacts/results/ (MD, CSV, JSON).

set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

EXE="./VibeMQ.Benchmarks"
if [[ ! -x "$EXE" ]]; then
  echo "Error: $EXE not found or not executable. Run from publish directory." >&2
  exit 1
fi

# Optional: first arg ending with .txt or .log -> tee output there
LOG_FILE=""
if [[ -n "$1" && ("$1" == *.txt || "$1" == *.log) ]]; then
  LOG_FILE="$1"
  shift || true
fi

# Run without interactive prompt (--filter selects StorageBenchmarks)
if [[ -n "$1" && "$1" != "--" ]]; then
  FILTER="*StorageBenchmarks*$1*"
else
  FILTER="*StorageBenchmarks*"
  shift || true
  [[ "$1" == "--" ]] && shift || true
  if [[ -n "$1" ]]; then
    FILTER="*StorageBenchmarks*$1*"
    shift || true
  fi
fi

if [[ -n "$LOG_FILE" ]]; then
  echo "Console output also saved to: $LOG_FILE" >&2
  exec "$EXE" --filter "$FILTER" "$@" 2>&1 | tee "$LOG_FILE"
else
  exec "$EXE" --filter "$FILTER" "$@"
fi
