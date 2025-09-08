# Shardis Migration Execution Sample

End-to-end demonstration of a shard key migration: planning, executing (copy → verify → swap), and reporting progress using in-memory components.

## What it shows

* Constructing source & target topology snapshots
* Creating a deterministic migration plan
* Executing copy + interleaved verify + batched swap phases
* Progress reporting (throttled) via `IProgress<MigrationProgressEvent>`
* Summary statistics (planned / done / failed / elapsed)
* Configuration of concurrency & batching (`ShardMigrationOptions`)
* Transient failure injection + automatic retry
* Cancellation mid-flight and resume from checkpoint (idempotent plan re-run)
* Large plan (100 keys) illustration with periodic progress sampling

## Running

```bash
dotnet run --project samples/Shardis.Migration.Sample
```

Expected output sections:

1. Basic execution (2 moves)
2. Transient failure + retry (simulated first-attempt copy failure)
3. Cancellation + resume (first run cancels after initial copy, second run resumes to completion)
4. Large plan (100 keys) with aggregated progress every ~10 copies

Sample snippet:

```text
=== 1. Basic execution ===
Plan <id> created with 2 moves
... progress ...
Planned=2 Done=2 Failed=0 Elapsed=00:00:00.xxxxx

=== 2. Transient failure + retry ===
(retry) copied=0 failed=0 retries activeCopy=1
Retry plan complete: Planned=2 Done=2 Failed=0

=== 3. Cancellation + resume ===
Cancel plan moves=4
Execution canceled (expected).
Resuming...
Resume complete: Done=4 Failed=0

=== 4. Large plan (100 keys) ===
(large) copied=0 verified=0 swapped=0
Large summary: Done=67 Failed=0 Elapsed=00:00:00.xxxxx
```

## Notes

* Uses in-memory planner, mover, verification & checkpoint store (no external durability) — replace for production.
* Failure injection implemented via a local decorator `FailureInjectingMover` (no change to core library).
* Cancellation + resume works because checkpoints persist after each flush threshold; re-running the same plan continues remaining moves.
* Large plan shows that not all keys require movement (67/100 moved in example) — planner only emits necessary moves.
* Tune `CopyConcurrency`, `VerifyConcurrency`, `SwapBatchSize` according to throughput vs resource trade-offs.
* For durability and observability implement a custom `IShardMigrationCheckpointStore` and `IShardMigrationMetrics`.
