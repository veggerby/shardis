# 📊 Shardis Benchmarks

This document captures the purpose and guidance for running and interpreting Shardis performance benchmarks.

## Goals

1. Track routing latency & allocations across implementations.
2. Compare ring hash algorithms (Default vs FNV-1a vs future variants).
3. Evaluate replication factor impact on consistent hashing performance.
4. Provide regression guardrails before releasing changes.

## Projects

Benchmark sources live under `benchmarks/`:

- `RouterBenchmarks` – compares Default vs Consistent hashing routers when routing 10k keys.
- `HasherBenchmarks` – micro-benchmarks ring hashers over 50k random values.

## Running

From repo root:

```bash
dotnet run -c Release -p benchmarks/Shardis.Benchmarks.csproj --filter *RouterBenchmarks*
dotnet run -c Release -p benchmarks/Shardis.Benchmarks.csproj --filter *HasherBenchmarks*
```

Add `--runtimes net8.0` (default) or include additional TFMs if supported in future.

## Typical Output Columns

| Column | Meaning |
|--------|---------|
| Mean | Average time per operation (ns, μs, ms depending on scale) |
| Error / StdDev | Statistical variance – instability indicator |
| Gen0/1/2 | Number of GC collections per 1k operations |
| Allocated | Bytes allocated per operation |

Lower Mean & Allocated plus zero Gen1/Gen2 are desired for hot paths.

## Tuning Tips

- Increase `ReplicationFactor` cautiously: better distribution vs more ring entries.
- Prefer cheaper ring hash algorithms if distribution remains acceptable.
- Watch allocations: accidental boxing or LINQ in hot path can inflate costs.

## Adding New Benchmarks

1. Create a new `*.cs` file in `benchmarks/`.
2. Reference `Shardis` via project reference (already in csproj).
3. Use `[MemoryDiagnoser]` and consider `[ThreadingDiagnoser]` for concurrency cases.
4. Keep payload deterministic to reduce noise (seeded RNG, prebuilt arrays).

## Roadmap Ideas

- Bench streaming query merge enumerators (ordered vs unordered).
- Benchmark migration planning overhead.
- Benchmark map store implementations (InMemory vs Redis vs SQL).

## Interpreting Changes

When diffs show >10% regression in Mean or >2x allocation increase:

- Re-assess code changes.
- Consider caching, preallocation, or struct value objects.
- Add targeted micro-benchmarks for new hotspots.

---

Last updated: 2025-08-25
