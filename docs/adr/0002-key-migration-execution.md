# ADR 0002: Key Migration Execution Model

**Status:** Accepted
**Date:** 2025-08-26
**Author:** Jesper Veggerby
**Supersedes:** None
**Superseded by:** None
**Related:** [ADR 0001](./0001-core-architecture.md) (Core Architecture of Shardis), [Backlog](../backlog.md)

---

## Context

Shardis supports map-based routing of keys across a set of shards. When the shard topology changes (scale-out, scale-in, rebalance), keys must be deterministically redistributed to preserve availability and correctness.

A naive migration risks:

* Inconsistent visibility (some requests hitting old shard, some new)
* Duplicate moves if the process is retried
* Unbounded retries on transient failures
* Loss of observability into progress

Therefore, Shardis requires a **resumable, idempotent, and observable migration mechanism** with strong invariants.

---

## Decision

Shardis will implement a **Key-level Migration Execution Model** with the following design elements:

### 1. Plan & State Machine

* **Plan:** Immutable ordered list of `KeyMove` items `{Key, Source, Target}`.
* **States:** `Planned → Copying → Copied → Verifying → Verified → Swapping → Done | Failed`.
* **Invariants:**

  1. A key’s assignment changes at most once per migration (atomic swap).
  2. No key marked `Done` without successful verification (unless force override).
  3. Re-running with the same plan is idempotent.
  4. Partial failures never expose dual-mapping; either old or new visible, never both.

### 2. Interfaces

* `IShardMigrationPlanner` → builds `MigrationPlan` from old/new topology.
* `IShardDataMover` → provider-specific `CopyAsync`, `VerifyAsync`.
* `IShardMapSwapper` → atomic reassignment of keys in batches.
* `IShardMigrationCheckpointStore` → persist/resume migration state.
* `IShardMigrationMetrics` → counters & gauges (extend `IShardisMetrics`).

### 3. Checkpoints & Idempotency

* **Checkpoint**: `{PlanId, UpdatedAtUtc, KeyStates, LastProcessedIndex}`.
* Checkpoints persisted **after every state transition beyond `Copying`** and periodically (configurable) to bound re-work.
* Per-key states maintained; for very large keyspaces, implement *segmented checkpoints* or *range cursors* (future extension).

### 4. Concurrency & Parallelism

* Configurable copy/verify concurrency (`CopyConcurrency`, `VerifyConcurrency`).
* Backpressure via semaphores.
* Swap executed in **batches** (e.g. 500 keys). All-or-nothing guarantee per batch.

### 5. Verification

* Deterministic provider-specific strategies:

  * Full equality check
  * Hash comparison
  * Sampled/hash-only (for large data sets)
* Configurable `VerificationMode`.

### 6. Failure Handling

* **Transient (network/timeouts):** retry with exponential backoff up to `MaxRetries`.
* **Permanent (not found at source):** mark `Failed`, include in summary.
* **Verification mismatch:** retry; if persistent, leave `Failed` unless override.
* **Swap failure:** retry batch; if unrecoverable, rollback batch to `Copied`.

### 7. Observability

* Metrics:

  * Counters: `KeysPlanned`, `KeysCopied`, `KeysVerified`, `KeysSwapped`, `KeysFailed`, `Retries`.
  * Gauges: `ActiveCopyConcurrency`, `ActiveVerifyConcurrency`.
* Progress stream (`IProgress<MigrationProgressEvent>`) emitted periodically.
* Logs redact key values; optional redaction for shard IDs.

### 8. Configuration Defaults

```csharp
new ShardMigrationOptions {
    CopyConcurrency = 32,
    VerifyConcurrency = 32,
    SwapBatchSize = 500,
    MaxRetries = 5,
    RetryBaseDelay = TimeSpan.FromMilliseconds(100),
    InterleaveCopyAndVerify = true,
    EnableDryRunHashSampling = true,
    ForceSwapOnVerificationFailure = false
}
```

### 9. Testing Strategy

* **Unit:** state transitions, idempotency, retries.
* **Integration:** end-to-end with in-memory mover.
* **Property tests:** same plan run twice = identical outcome.
* **Fault injection:** simulate transient vs permanent errors.
* **Performance:** scaling benchmarks vs concurrency.

### 10. Extensibility

* Provider-specific movers (Marten, EF, SQL).
* Pluggable verification strategies.
* Custom retry policies (`ITransientFaultPolicy`).
* Operator UI integration via progress listener.

---

## Consequences

### Positive

* Deterministic, resumable migration process.
* Operator visibility into progress & failures.
* Clean separation of concerns (planner, mover, swapper, checkpoint).
* Extensible for multiple providers and verification modes.

### Negative

* Per-key checkpoints may not scale for extremely large keyspaces (requires future optimization).
* Assumes shard map store can support atomic multi-key swaps; weaker providers need compensation logic.
* Verification can be costly; may require sampled mode in production.

---

## Unresolved Issues

* **Q1:** Should migration metrics be integrated into the existing `IShardisMetrics` interface, or defined separately for migration?
* **Q2:** Should the migration plan ordering optimize for locality (e.g., grouping moves by `(Source, Target)` to reduce thrash)?
* **Q3:** What is the long-term strategy for extremely large key sets (streaming plans, segmented checkpoints, range cursors)?
* **Q4:** How can topology snapshots and shard map updates be captured and restored atomically across the map store and routing ring?

---

## Alternatives Considered

* **Dual-read at query time:** rejected; adds latency and complexity to read path.
* **Transactional multi-shard writes:** rejected; overly strong guarantees not portable across providers.
* **Single-phase copy without verify:** rejected; weak safety, no correctness guarantee.

---

## Status & Next Steps

* **Accepted for implementation pre-1.0.**
* Early prototype will implement:

  * In-memory checkpoint store
  * In-memory data mover
  * Metrics counters
* Future ADR will cover optimization for **streaming/segmented checkpoints**.

---

## Appendix: State Diagram (Textual)

```text
Planned → Copying → Copied → Verifying → Verified → Swapping → Done

Failure paths:
Copying (transient) → retry Copying → Failed
Verifying (mismatch) → retry Verifying → Failed
Swapping (partial failure) → retry Swapping → rollback → Copied | Failed
```
