# Shardis.Migration.Advanced.Sample

Purpose: Illustrate production-adjacent migration patterns that are intentionally omitted from the basic EntityFrameworkCore sample to keep it approachable.

## Included Concepts (Planned)

* Streaming / segmented planning over `IAsyncEnumerable<ShardMap<TKey>>` without full key materialization.
* Weighted balancing heuristic (target proportional to observed shard weights / load metrics) minimizing key movement.
* Adjustable execution concurrency (copy / verify) with simple back-pressure example.
* Retry & abort policy (exponential backoff, fail-fast threshold, summary of skipped moves).
* Drift detection with topology version hash checkpointing per segment.
* Metrics wiring example (custom `IShardisMetrics` implementation + minimal counters) and OpenTelemetry bridge sketch.

## Not Implemented Yet

This folder is a placeholder; code will be added incrementally. Keeping a README now allows linking from primary sample.

## When To Use These Patterns

Use when:

* Key count >> memory capacity (millions+)
* Rebalance needs to minimize moved data (cost, cache locality)
* Continuous write workload causes potential topology drift during planning
* Observability / SLOs demand metrics & partial failure handling

## Roadmap

1. Segmented planner prototype (ordered ranges) + tests.
2. Weight-based target topology builder.
3. Concurrency / retry surface on executor options.
4. Metrics adapter & sample dashboard snippet.
5. Documentation pass integrating with main docs site.

---
For current state see: <https://github.com/veggerby/shardis>
