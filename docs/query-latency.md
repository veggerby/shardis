# Query Merge Latency Metrics

This document describes the unified latency instrumentation for cross-shard queries.

## Instrument

Histogram (OpenTelemetry): `shardis.query.merge.latency` (unit: `ms`).

Semantics: Wall-clock duration from fan-out start (first shard enumeration attempt) until the merged async enumeration completes (success, cancellation, or failure). Emitted exactly once per query enumeration (ordered and unordered paths share a single emission point).

## Invariants

1. Single emission per logical enumeration.
2. Emission always occurs (including failure, cancellation, zero-result, and all-invalid shard targeting).
3. Tag cardinality is bounded: only documented keys (below) + low-range integers (counts) and short provider/type names.
4. Ordered (buffered) executions reuse the unordered executor’s captured context; the buffered ordering cost is included in the measured duration.
5. All-invalid targeting (every supplied shard id rejected) yields: `target.shard.count=0`, `invalid.shard.count>0`, `result.status=ok` with zero results.

## Tag Schema

| Tag | Description |
|-----|-------------|
| `db.system` | Storage system (sqlite, postgresql, mssql, mysql, other, or blank if undetected). |
| `provider` | Logical provider (e.g. `efcore`). |
| `shard.count` | Total configured shards. |
| `target.shard.count` | Shards successfully targeted (after validation / de-dup). |
| `invalid.shard.count` | Rejected targeted shard ids (parse / range failures). Zero when none. |
| `merge.strategy` | `unordered` or `ordered`. |
| `ordering.buffered` | `true` for current ordered EF Core buffered path, otherwise `false`. |
| `fanout.concurrency` | Effective parallel shard enumerations (≤ targeted shard count and ≤ configured limit). |
| `channel.capacity` | Unordered merge channel capacity; `-1` when unbounded or not applicable. |
| `failure.mode` | `fail-fast` or `best-effort` (best-effort: partial shard failures suppressed; emits `ok` if ≥1 shard succeeded, otherwise `failed`). |
| `result.status` | One of: `ok`, `canceled`, `failed`. |
| `root.type` | Short CLR type name of the query root. |

## Failure Mode Tag

Two values are presently emitted:

* `fail-fast` – first shard failure ends enumeration and reports `result.status=failed`.
* `best-effort` – shard failures are collected; enumeration continues. If at least one shard succeeds, the final `result.status=ok`; if all shards fail the histogram still emits with `result.status=failed`.

## Invalid Shard Targeting

`invalid.shard.count` counts the number of supplied shard identifiers that failed parsing or were out of range. If all supplied targets are invalid, the executor skips fan-out and immediately emits a zero-result histogram with `target.shard.count=0` (fast-path guard without errors).

## Recommended Views / Buckets

Apply an explicit bucket view if your backend defaults are unsuitable, e.g. `[5,10,20,50,100,200,500,1000,2000,5000]` milliseconds. Ensure aggregation temporality is cumulative (default) for most exporters.

## Correlated Tracing

ActivitySource: `Shardis.Query`. Per-query activity carries overlapping tags plus additional diagnostic events (e.g. invalid targeting). Use trace/span linkage to join latency metrics with richer per-shard timing or failure analysis.

## Backward Compatibility

All tags listed are considered stable. New tags, if added, will be appended (never repurposed). Consumers should ignore unknown tags.

## Future Work

* Streaming ordered merge path (buffered path will remain but be distinguishable).
* Allocation regression guard around latency emission path.

See also ADR 0006 (Unified Query Latency Single-Emission Model) for design rationale.

---
For an overview see the provider README (`Shardis.Query/README.md`).
