# Shard Migration – Design, Guarantees & Roadmap

This document formalizes the approach for safely reassigning keys (and associated data) between shards.

---
## Objectives

1. Deterministic, auditable reassignment of a bounded key set.
2. Zero silent data loss (copy then cut‑over, never destructive first).
3. Idempotent operations – re-running a plan does not duplicate work.
4. Minimal routing disruption (existing keys keep mapping until commit).
5. Progress visibility + metrics (planned vs migrated vs failed keys).

---
## Core Types (Current / Planned)

| Type | Status | Purpose |
|------|--------|---------|
| `IShardMigrator<TKey,TSession>` | ✅ (scaffold) | Plans & executes key moves. |
| `DefaultShardMigrator<TKey,TSession>` | ✅ | Baseline implementation (no data copy yet). |
| `ShardMigrationPlan<TKey>` | ✅ | Immutable plan: source, target, key set. |
| `IShardDataTransfer<TKey,TSession>` | 🚧 | Backend-specific data fetch + write abstraction. |
| `IShardMigrationAuditor` | 🚧 | Records events (start, key copied, committed, failed). |
| `IShardMigrationLocker` | 🚧 | Optional: coordinate concurrent migrations (advisory lock). |

---
## High-Level Flow (Target State)

1. Discover candidate keys (external query or supplied list).
2. `PlanAsync` produces `ShardMigrationPlan<TKey>`.
3. (Optional) Dry-run validation: ensure all keys currently map to source.
4. Execute in phases per key:
   1. Read entity/data from source (`IShardDataTransfer.ReadAsync`).
   2. Write entity/data to target (`IShardDataTransfer.WriteAsync`).
   3. Verify write (checksum / count / version match).
   4. Swap mapping in map store (atomic / compare-and-set if supported).
   5. (Optional) Mark old copy for deferred purge after grace period.
5. Emit metrics + audit events.

---
## Idempotency Strategy

| Step | Idempotency Mechanism |
|------|-----------------------|
| Read | Pure fetch – no side effects. |
| Write | Upsert with deterministic key; checksum or version match short-circuits repeats. |
| Verify | Safe to re-run; recomputes checksum. |
| Mapping swap | Only update if current mapping == source (guard against races). |
| Cleanup | Deferred; can rely on TTL / explicit flag. |

If a run crashes mid-key, re-execution resumes at first non-idempotent boundary automatically.

---
## Data Integrity Guarantees

Goal tiering (progressively implemented):

| Tier | Guarantee |
|------|-----------|
| 0 (current) | Key map changes only; no data copy. |
| 1 | Copy & map swap is all-or-nothing per key (best-effort). |
| 2 | Per-key verification with checksum (size / hash). |
| 3 | Dual-read grace window (serve from old if new missing). |
| 4 | Cryptographic diff audit (optional). |

---
## Concurrency & Locking

Planned extension points:

- Shard-scoped mutex (prevent two migrations targeting same shard pair simultaneously).
- Key-range segmentation to allow parallel migrations across disjoint key sets.
- Backpressure knobs: max in-flight keys, retry budget.

---
## Metrics (Planned Names)

| Metric | Type | Description |
|--------|------|-------------|
| shardis.migration.plan.keys | Counter | Total keys scheduled. |
| shardis.migration.key.copied | Counter | Data copy completed. |
| shardis.migration.key.committed | Counter | Mapping swap done. |
| shardis.migration.key.failed | Counter | Key failed permanently. |
| shardis.migration.duration | Histogram | End-to-end key migration latency. |

Metrics emitted outside critical locks.

---
## Failure Handling

| Failure | Action |
|---------|--------|
| Read fails | Retry with exponential backoff (bounded). |
| Write conflict | Re-read + idempotent upsert. |
| Mapping race | Abort key; re-plan or surface conflict. |
| Verification mismatch | Mark key failed; do not switch mapping. |

No partial destructive operations; mapping change is final boundary.

---
## Dry Run Mode

`ExecutePlanAsync(plan, dryRun: true)` (planned signature) will:

- Validate current map assignments.
- Simulate hash distribution post-move.
- Produce summary (keys ok, mismatched, duplicates, already moved).
- Emit no writes or mapping changes.

---
## Open Questions

1. Should mapping store expose compare-and-set primitive? (Recommended for atomicity.)
2. Provide generic serialization helpers or delegate entirely to domain?
3. Do we support batch commit for large contiguous key sets? (Avoid long tail.)
4. Graceful rollback path if mapping swap occurs but verification fails late? (Prefer pre-swap verification to avoid.)

---
## Near-Term Implementation Tasks

1. Introduce `IShardDataTransfer<TKey,TSession>` + default noop (test harness).
2. Extend `IShardMapStore` with optional compare-and-set (`TryReassign(shardKey, expectedSource, newTarget)`).
3. Implement per-key pipeline with structured result object.
4. Add migration metrics instrumentation wrapper.
5. Provide unit tests for idempotency + mapping race.
6. Add dry-run compute & report.

---
## Usage (Current Scaffold)

```csharp
var migrator = new DefaultShardMigrator<string, string>();
var plan = await migrator.PlanAsync(sourceShard, targetShard, keys);
await migrator.ExecutePlanAsync(plan, key => Task.CompletedTask); // callback per key
```

Future (planned):

```csharp
var result = await migrator.ExecutePlanAsync(plan, options => options
    .WithDryRun()
    .WithMaxParallelism(8)
    .WithMetrics(metrics)
    .OnKeyCommitted(k => logger.LogInformation("Committed {Key}", k)));
```

---
## Summary

Migration will emphasize *determinism*, *auditability*, and *incremental adoption*. This scaffold provides a foundation without risking data until full copy + verify pipeline is implemented.
