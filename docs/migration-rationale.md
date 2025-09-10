# Migration Rationale

Why Shardis includes an explicit key-level migration subsystem.

## 1. Problem Statement

Sharded systems evolve: new shards are added to scale out; shards may be decommissioned or rebalanced to reduce hotspots; tenancy distribution may need to reflect geographic or cost constraints. Without a disciplined migration mechanism, reassigning keys risks:

* Inconsistent reads/writes (some requests routed to old shard, some to new) leading to divergence.
* Duplicate copy effort and partial moves after interruptions.
* Lost progress on crash (no durable resume point) causing extended maintenance windows.
* Silent data loss or corruption when verification is ad‑hoc or skipped for performance.
* Operational opacity: teams cannot estimate remaining time or blast radius.

## 2. When Migration Is Needed

Trigger scenarios:

* Scale out: adding shards to reduce per‑shard load / storage.
* Scale in / consolidation: retiring underutilized shards (cost optimization).
* Rebalance after skew: a subset of keys dominates traffic or storage.
* Topology mutation for regulatory or geographic partitioning.
* Hash function / virtual node distribution change (ring weight adjustment).
* Disaster recovery reshaping (failover where replacement shards must assume key ranges).

## 3. Goals

| Goal | Description |
|------|-------------|
| Determinism | Same inputs (topology A→B, key set) produce identical plan & execution path. |
| Idempotency | Safe resume after interruption without duplicating verified work. |
| Observability | Real‑time counters & progress to drive dashboards + alerts. |
| Safety | Guarantee no dual authoritative mapping (old/new simultaneously). |
| Extensibility | Pluggable movers, verification strategies, checkpoint stores. |
| Bounded Work | Avoid unbounded memory / retry storms; configurable concurrency & checkpoints. |
| Minimal Blast Radius | Failures localized to specific keys/batches. |

## 4. Non‑Goals

| Non‑Goal | Rationale |
|----------|-----------|
| Cross-shard distributed transactions | Complexity + portability; swaps are atomic at shard map level only. |
| Automatic topology sizing | External capacity planner decides; executor consumes a declared target. |
| Multi‑cluster/global replication | Out of scope; handle at infrastructure or application replication layer. |
| Fine-grained per-row conflict resolution | Migration assumes exclusive write routing per key. |

## 5. Invariants

These invariants (also codified in ADR‑0002) must hold for correctness:

1. A key’s authoritative shard changes at most once per migration plan.
2. No key is marked Done without successful verification unless an explicit unsafe override (`ForceSwapOnVerificationFailure`) is enabled.
3. At any time during execution or after crash/restart, each key is visible on exactly one shard for routing decisions.
4. Re-running the same plan (same PlanId + ordered moves) yields identical final assignments if no external mutations occur.
5. Verification strategy outcomes depend only on deterministic inputs (no hidden randomness).

## 6. Architectural Shape

Key components and responsibilities:

| Component | Role |
|-----------|------|
| Planner (`IShardMigrationPlanner`) | Diff current vs target topology → ordered `MigrationPlan`. |
| Executor (`ShardMigrationExecutor`) | Drives per-key state machine, concurrency, retries, progress emission. |
| Data Mover (`IShardDataMover`) | Idempotent copy + (optional) pre‑verification prep for a single key. |
| Verification Strategy (`IVerificationStrategy`) | Deterministic equality / checksum check. |
| Map Swapper (`IShardMapSwapper`) | Atomic batch assignment of verified keys. |
| Checkpoint Store (`IShardMigrationCheckpointStore`) | Durable state for resume. |
| Metrics (`IShardMigrationMetrics`) | Counters & gauges (backed by `IShardisMetrics`). |

State machine (summary): Planned → Copying → Copied → Verifying → Verified → Swapping → Done | Failed (see `migration-usage.md` diagrams).

## 7. Why Key-Level (vs Range / Bulk) Migration

| Approach | Advantages | Drawbacks |
|----------|------------|-----------|
| Key-level (chosen) | Fine-grained visibility, targeted retries, minimal failed rework | More metadata overhead for very large key counts |
| Range-based | Compact metadata | Hard to express skewed key hotness; complex partial failures |
| Bulk (full shard clone) | Simple conceptually | High resource spike; poor control over ordering & progress granularity |

Segmented / range-aggregate checkpoints are a documented future optimization (ADR‑0004) to mitigate overhead when key count is extreme.

## 8. Failure Handling Strategy

| Failure Type | Detection | Action |
|--------------|----------|--------|
| Transient copy/verify (timeout, network) | Exception classification | Exponential backoff retry (respect MaxRetries) |
| Permanent copy (missing source key) | Specific exception | Mark Failed; do not retry |
| Verification mismatch | Compare result | Retry (configurable) then Failed unless override |
| Swap partial failure | Swapper exception | Retry batch; on irrecoverable failure, leave keys Copied or mark Failed |

## 9. Observability Model

Metrics (counters & gauges) + progress events provide three SLO signals:

* Throughput (keys/minute) – derivative of Copied/Verified/Swapped counters over time window.
* Failure ratio – Failed / (Planned) threshold triggers alerts.
* Staleness – time since last progress event; indicates stall or crash.

Operators can derive ETA by linear projection of Verified or Swapped vs time if throughput stable.

## 10. Operational Guidance

| Scenario | Recommendation |
|----------|---------------|
| Large plan (millions of keys) | Tune checkpoint thresholds higher; consider segmented planner once available. |
| Latency-sensitive system | Lower copy concurrency; enable interleaving to smooth total elapsed time. |
| Limited IO budget on target | Stagger concurrency (lower verify vs copy) to reduce read amplification. |
| Hot shard removal | Prioritize plan ordering grouping hot keys earlier for faster relief (custom planner extension). |
| Retry storms | Inspect exception taxonomy; refine transient vs permanent classification. |

## 11. Alternatives Considered (Condensed)

* Dual-writes / dual-reads phase: higher application complexity & latency; rejected.
* Lazy migration on first access: unpredictable completion, operational opacity; rejected.
* Global transaction wrapper: portability & performance concerns; rejected.

## 12. Future Work

* Segmented checkpoints & streaming plan ingestion (ADR‑0004).
* Adaptive concurrency based on observed latency percentile windows.
* Progressive verification strategies (sample → hash → full) under time budget.
* Planner heuristics for locality grouping to reduce shard thrash.

## 13. Cross References

* `migration-usage.md` – practical usage & diagrams.
* `terms.md` – definitions (Key Move, Cutover, Checkpoint).
* ADR‑0002 – formal execution model invariants.
* ADR‑0004 – proposed segmentation strategy (future).

---

This rationale is stable; update when major migration semantics change or additional invariants emerge.
