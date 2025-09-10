# Shardis Benchmarks

This document describes the current benchmark suites, how to run them using BenchmarkDotNet’s native filtering & categories, and how to scale effort between quick CI signals and deeper local analysis.

## Goals

1. Track routing latency & allocations across implementations.
2. Compare ring hash algorithms (Default vs FNV-1a vs future variants).
3. Evaluate replication factor impact on consistent hashing performance.
4. Provide regression guardrails before releasing changes.

## Suites

Benchmark sources live under `benchmarks/` (single project `Shardis.Benchmarks`):

- `RouterBenchmarks` (category `router`) – Default vs Consistent routers routing 10k deterministic keys.
- `HasherBenchmarks` (category `hasher`) – ring hasher micro-benchmarks over 50k seeded random values.
- `MigrationThroughputBenchmarks` (category `migration`) – end-to-end migration executor throughput across a controlled concurrency matrix.
- `BroadcasterStreamBenchmarks` (category `broadcaster`) – evaluates the existing streaming broadcaster under shard speed skew (fast vs slow producers) and varying item pacing. Includes a channel capacity sweep (0=unbounded, 32..512) and a consumer‑slow variant; ordered merge modes ignore capacity (N/A) by design.
- `MergeEnumeratorBenchmarks` (category `merge`) – compares three global merge strategies: unordered streaming (baseline), ordered streaming (bounded prefetch, low memory, early first-item), and ordered eager (parallel per-shard materialization, higher memory, potentially larger first-item delay). Also exports first‑item latency percentile CSV (p50/p95) grouped by parameter tuple.
- `MergeEnumeratorBenchmarks` (category `merge`) – compares three global merge strategies: unordered streaming (baseline), ordered streaming (bounded prefetch, low memory, early first-item), and ordered eager (parallel per-shard materialization, higher memory, potentially larger first-item delay). Also exports first‑item latency percentile CSV (p50/p95) grouped by parameter tuple.
- `SegmentedPlannerBenchmarks` (category `plan`) – compares in-memory vs segmented enumeration migration planning (includes optional dry-run counts) across key counts and segment sizes; allocation focus.

## Running

Entry point uses `BenchmarkSwitcher`; you can select by category or filter. From repo root:

Quick (default migration only – no args):

```bash
dotnet run -c Release --project benchmarks/Shardis.Benchmarks.csproj --
```

List available benchmarks (tree form):

```bash
dotnet run -c Release --project benchmarks/Shardis.Benchmarks.csproj -- --list tree
```

By category:

```bash
dotnet run -c Release --project benchmarks/Shardis.Benchmarks.csproj -- --anyCategories router
dotnet run -c Release --project benchmarks/Shardis.Benchmarks.csproj -- --anyCategories hasher
dotnet run -c Release --project benchmarks/Shardis.Benchmarks.csproj -- --anyCategories migration
dotnet run -c Release --project benchmarks/Shardis.Benchmarks.csproj -- --anyCategories broadcaster
dotnet run -c Release --project benchmarks/Shardis.Benchmarks.csproj -- --anyCategories migration,router
dotnet run -c Release --project benchmarks/Shardis.Benchmarks.csproj -- --anyCategories plan
```

By type/name filter:

```bash
dotnet run -c Release --project benchmarks/Shardis.Benchmarks.csproj -- --filter *RouterBenchmarks*
```

Add additional runtimes (future multi-target):

```bash
dotnet run -c Release --project benchmarks/Shardis.Benchmarks.csproj -- --runtimes net8.0
```

### Environment Controls

| Variable | Values | Effect |
|----------|--------|--------|
| `SHARDIS_FULL` | `1` / unset | Expands migration parameter matrix when `1`; small default otherwise. CI ignores expansion. |
| `SHARDIS_BENCH_MODE` | `full` / unset | When `full`, uses longer run (IterationCount=15, WarmupCount=5); otherwise quick signal (3/3). |
| `SHARDIS_PLAN_KEYS` | integer (e.g. 1000000) | Overrides max key count for segmented planner benchmarks (cap). |

Examples:

```bash
# Full migration matrix, deeper statistical rigor
SHARDIS_FULL=1 SHARDIS_BENCH_MODE=full dotnet run -c Release --project benchmarks/Shardis.Benchmarks.csproj -- --anyCategories migration

# Quick router benchmark
dotnet run -c Release --project benchmarks/Shardis.Benchmarks.csproj -- --anyCategories router
```

## Typical Output Columns

| Column | Meaning |
|--------|---------|
| Mean | Average time per operation (ns, μs, ms depending on scale) |
| Error / StdDev | Statistical variance – instability indicator |
| Gen0/1/2 | Number of GC collections per 1k operations |
| Allocated | Bytes allocated per operation |

Lower Mean & Allocated plus zero Gen1/Gen2 are desired for hot paths.

### MigrationThroughputBenchmarks

Default (small) matrix to keep local & CI cycles short:

| Keys | CopyConcurrency | VerifyConcurrency | Interleave | SwapBatchSize |
|------|-----------------|------------------|------------|---------------|
| 10k  | 1,4             | 1,4              | true       | 100           |

Expanded matrix when `SHARDIS_FULL=1` (local exploratory):

| Keys          | CopyConcurrency    | VerifyConcurrency | Interleave        | SwapBatchSize      |
|---------------|--------------------|-------------------|-------------------|--------------------|
| 1k,10k,100k   | 1,4,16             | 1,4,16            | true,false        | 10,100,1000        |

Baseline semantics: BDN baseline is the first row (Copy=1, Verify=1) for ratio computation; only one benchmark method is needed.

### SegmentedPlannerBenchmarks

Parameters (quick mode):

| KeyCount | Moves | SegmentSize |
|----------|-------|-------------|
| 10k,100k | 5k    | 5k,10k,25k  |

Parameters (full mode or when `SHARDIS_PLAN_KEYS>=1000000`): adds `1_000_000` keys (ensure sufficient memory; dry-run is cheap, full plan allocations scale with #moves).

Benchmarked Methods:

| Method | Description | Allocation Profile |
|--------|-------------|--------------------|
| InMemoryPlanner | Full materialization diff | O(N) snapshot + O(M) moves |
| SegmentedPlanner | Streaming enumeration + diff | O(segmentSize + M) |
| SegmentedDryRun | Counts only (Examined, Moves) | O(1) (ignores move list) |

Interpretation:

- Prefer segmented planner when key count >> available memory headroom; dry-run first to estimate move ratio.
- High move ratio (>30–40%) narrows allocation gap between strategies because move list dominates either path.
- Segment size too small: higher CPU (diff overhead). Too large: increased transient batch memory. Tune where allocation flattening + throughput balance.

Example:

```bash
SHARDIS_BENCH_MODE=full SHARDIS_PLAN_KEYS=1000000 dotnet run -c Release --project benchmarks/Shardis.Benchmarks.csproj -- --filter *SegmentedPlannerBenchmarks*
```

Key Metric Focus: Allocated (bytes/op) and Gen0 GC count; time difference usually dominated by enumeration cost (I/O in real stores) rather than diff mechanics.

Exporters: JSON (`*-report-full.json`) and GitHub Markdown are emitted under `BenchmarkDotNet.Artifacts/results/`. A duplicate exporter warning may appear if BDN auto-adds Markdown; harmless.

CI Guidance: Quick job (3 warmup / 3 iteration) keeps runtime bounded. For deeper analysis set `SHARDIS_BENCH_MODE=full`.

Diffing Strategy: Store a prior JSON (e.g. copy to `benchmarks/results/migration-last.json`) then compare new run:

```bash
jq '.Benchmarks[] | {case:.FullName, mean:.Statistics.Mean}' BenchmarkDotNet.Artifacts/results/Shardis.Benchmarks.MigrationThroughputBenchmarks-report-full.json > current.json
diff -u benchmarks/results/migration-last.json current.json || true
cp current.json benchmarks/results/migration-last.json
```

Interpretation:

- Throughput improves with concurrency until coordination/scheduling overhead flattens or regresses.
- Large `SwapBatchSize` reduces swap overhead but can inflate individual operation latency; watch GC + variability.
- Non-interleaved mode (when enabled in full matrix) isolates phases and typically reduces overlap efficiency.

## Tuning Tips

- Increase `ReplicationFactor` cautiously: better distribution vs more ring entries.
- Prefer cheaper ring hash algorithms if distribution remains acceptable.
- Watch allocations: accidental boxing or LINQ in hot path can inflate costs.
- Broadcaster channel capacity: extremely low capacities (≤32) can raise backpressure waits & p95 first-item latency under harsh skew; moderate (128–256) often balances memory and latency; unbounded (0) removes waits but may increase allocation & defer cancellation responsiveness.

### Capacity Tuning Quick Defaults

| Scenario | Recommended Capacity | Notes |
|----------|----------------------|-------|
| Balanced mix, mild skew | 128–256 | Good latency/memory tradeoff |
| Memory constrained | 64 | Expect more waits & elevated p95 under harsh skew |
| High skew / bursty producers | 256 | Reduces backpressure waits without going fully unbounded |
| Consumer also slow | 256–512 | Extra buffering smooths simultaneous producer+consumer stalls |
| Lowest latency focus & ample memory | 512 or 0 (unbounded) | Minimizes waits; watch allocations & GC |

Consumer‑slow benchmark variant helps illustrate compounded backpressure when both producer pacing and consumer drain speed interact; capacity above 256 typically dampens wait amplification in that case.

## Adding New Benchmarks

1. Create a new `*.cs` file in `benchmarks/`.
2. Reference `Shardis` via project reference (already in csproj).
3. Use `[MemoryDiagnoser]` and consider `[ThreadingDiagnoser]` for concurrency cases.
4. Keep payload deterministic to reduce noise (seeded RNG, prebuilt arrays).

## Roadmap Ideas

- (moved to active) Streaming merge enumerators now implemented: see `MergeEnumeratorBenchmarks`.
- Benchmark migration planning overhead. (Partially addressed via SegmentedPlannerBenchmarks.)
- Benchmark map store implementations (InMemory vs Redis vs SQL).
- Add broadcaster channel capacity sweep (param) for backpressure tuning.

## Interpreting Changes

When diffs show >10% regression in Mean or >2x allocation increase:

- Re-assess code changes.
- Consider caching, preallocation, or struct value objects.
- Add targeted micro-benchmarks for new hotspots.

---

Last updated: 2025-08-27
