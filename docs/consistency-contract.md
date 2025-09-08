# Shardis Migration Consistency Contract

This document defines the client-visible guarantees during shard key migrations.

## Goals

* No lost writes.
* Deterministic key routing pre/post swap.
* Bounded read staleness when dual-read disabled.
* Idempotent, restartable execution.

## Phases

1. Copy – target populated from source (may repeat).
2. Verify – integrity / version / checksum comparison.
3. Swap – shard map updated atomically (per key or batch) using optimistic concurrency.
4. (Optional) Cleanup – source tombstoned or deleted after safe window.

## Write Handling

Mode | Behavior | Trade-off
-----|----------|---------
Block (default) | Writes to a key in Copy/Verify briefly blocked until swap completes. | Simplest; short pause.
Dual-write | Writes applied to source + target until swap. | Higher write cost; no pause.
CDC | Bulk copy snapshot then apply captured deltas. | Lowest pause; infra complexity.

## Read Handling

* Default: Reads route to source until swap. After swap, route to target.
* Bounded staleness target: `ShardMigrationOptions.MaxReadStaleness` (default 2s p99) without dual-read.
* Dual-read enabled: API may compare source/target; fallback if mismatch; typical p99 < 200ms exposure window.

## Swap Atomicity

* Atomic per key (or batch) via optimistic update: WHERE key=@k AND shard_id=@from AND version=@expected.
* History recorded for audit & replay.

## Failure / Resume

* Copy/verify operations retried with capped exponential backoff.
* Checkpoints written periodically; on restart, executor resumes at last persisted state.
* Failed keys isolated; summary exposes counts; subsequent plan may target failures only.

## Projection & Schema Evolution

* Optional projection strategy can reshape entities during copy (e.g., v1 -> v2).
* Verify must operate on post-projection representation.

## Observability

Metrics (suggested):

* `shardis.migration.keys_copied_total`
* `shardis.migration.keys_verified_total`
* `shardis.migration.keys_swapped_total`
* `shardis.migration.keys_failed_total`
* Duration histograms for copy / verify / swap


Spans: copy, verify, swap (attributes: key hash, source, target, backend, attempt, success)

## Configuration Surface (Selected)

Option | Purpose | Default
-------|---------|--------
CopyConcurrency | Max concurrent copy operations | 32
VerifyConcurrency | Max concurrent verify operations | 32
SwapBatchSize | Keys swapped per batch | 500
MaxConcurrentMoves | Soft global budget (governor hint) | null (derived)
MaxMovesPerShard | Per-shard budget (governor hint) | null (derived)
HealthWindow | Observation window for throttling | 5s
EnableDualRead | Use dual-read strategy for reads in-flight | false
EnableDualWrite | Duplicate writes to target during copy | false
MaxReadStaleness | p99 staleness bound without dual-read | 2s

## Client Guidance

* Idempotent writes recommended during migration windows.
* Long-running read operations should capture shard map version to detect swap mid-flight if strict consistency required.
* Prefer eventual cleanup (lazy source delete) to minimize swap critical section time.

## Non-Goals

* Global transactions across shards.
* Cryptographic verification (non-crypto hashes used for speed).

---

For deeper architectural rationale see ADR 0002.
