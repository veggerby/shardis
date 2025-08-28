# ADR-0004 — Segmented Planner & Checkpoint Ranges

**Status:** Proposed
**Date:** 2025-08-28
**Author:** Jesper Veggerby
**Supersedes:** None
**Superseded by:** None

---

## Context

Large keyspaces and long-running migrations require:

* **O(segments)** memory (not O(keys))
* **Resumability** via idempotent checkpoints
* **Verification** through range-wise invariants + end-to-end audit

Existing planners materialise too much state when keys are large or skewed. Long migrations are vulnerable to partial failures and need a way to resume without re-planning the entire keyspace.

---

## Decision

Introduce a **segmented planning model**:

* Partition the keyspace into contiguous, durable **segments** (ranges).
* Planner produces a plan as an ordered sequence of **SegmentMoves**.
  Each `SegmentMove` describes source range, destination shard(s), and metadata for execution.
* Each segment move is associated with a durable **CheckpointId**.
* Executor applies moves one (or N concurrently), committing progress per segment into a checkpoint store.
* Executor supports **resume-from-checkpoint**, skipping completed segments.
* Expose a `SegmentConcurrency` option to tune parallelism and backpressure.

### API Sketch

```csharp
public readonly record struct SegmentRange<TKey>(TKey StartInclusive, TKey EndExclusive);

public readonly record struct SegmentMove<TKey>(
    SegmentRange<TKey> Range,
    ShardId From,
    ShardId To,
    CheckpointId Id);

public interface ICheckpointStore
{
    Task SaveCheckpointAsync(
        CheckpointId id,
        SegmentMove move,
        CheckpointState state,
        CancellationToken ct);

    Task<CheckpointState?> LoadCheckpointAsync(
        CheckpointId id,
        CancellationToken ct);
}

public interface ISegmentedPlanner<TKey>
{
    IAsyncEnumerable<SegmentMove<TKey>> CreateSegmentPlanAsync(
        TopologySnapshot<TKey> from,
        TopologySnapshot<TKey> to,
        PlannerOptions options,
        CancellationToken ct);
}
```

**Durability & idempotency expectations:**

* Segment moves must be recorded atomically, or reconciliation must detect completed moves.
* Executor persists completed checkpoints and never re-executes recorded segments.

---

## Consequences

* Memory bounded by segment count, not key count.
* Partial progress is resumable → long migrations become robust.
* Requires checkpoint store abstraction and executor extensions.
* Enables verification hooks: per-segment invariants (counts/hashes) + final topology checks.

---

## Success Criteria

* Planning uses ≤ O(segments) memory.
* Executor resumes from last successful checkpoint.
* Verification: per-segment + final topology consistency.
* Executor exposes concurrency + per-segment metrics (progress, retries).

---

## Open Questions

* **Segment selection strategy:** uniform size, cardinality-based, or adaptive?
* **Checkpoint guarantees:** exactly-once vs. at-least-once?
* **Failure policy:** retry budget, poison-segment handling, visibility of failed segments?
* **Operator tooling:** inspection/repair of partial segments, re-run verification?

---

## Implementation Notes / Prototype Checklist

* Add `ISegmentedPlanner<TKey>` + `SegmentMove<TKey>` in `Shardis.Migration.Planning`.
* Add `ICheckpointStore` abstraction + `InMemoryCheckpointStore` for tests.
* Extend `ShardMigrationExecutor` to accept optional `CheckpointCursor` for resume.
* Add integration test: 10-segment plan, inject failures, assert resume to completion.
* Add metrics:

  * `shardis_migration_segment_completed_total`
  * `shardis_migration_segment_retries_total`
  * `shardis_migration_current_segment`
