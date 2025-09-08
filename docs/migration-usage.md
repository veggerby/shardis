# Shardis Migration Usage Guide

This guide shows how to register and execute a key migration using the in-memory reference components introduced in ADR 0002. It is intended for experimentation, tests, and as a template for production adapters.

> Production note: Replace the in-memory mover, checkpoint store, and (optionally) verification strategy with durable/provider-specific implementations before migrating real data.

## Overview

A migration moves keys whose target shard changes between two topology snapshots. The executor processes an ordered plan of `KeyMove` items with deterministic state transitions:

Planned → Copying → Copied → Verifying → Verified → Swapping → Done | Failed

Progress is observable via metrics and an optional `IProgress<MigrationProgressEvent>` stream.

## 1. Service Registration

Register Shardis core + migration services. The generic key type (e.g. `string`, `Guid`, `long`) must match your application key type.

```csharp
var services = new ServiceCollection();

// Core Shardis (assumes an extension exists; adjust as needed)
services.AddShardis<string>();

// Migration (in-memory defaults). Customize options if desired.
services.AddShardisMigration<string>(opt =>
{
    opt.CopyConcurrency = 64;      // increase parallel copy
    opt.VerifyConcurrency = 32;    // keep verification moderate
    opt.SwapBatchSize = 1000;      // larger batches reduce swap overhead
    opt.MaxRetries = 5;
    opt.RetryBaseDelay = TimeSpan.FromMilliseconds(100);
    opt.InterleaveCopyAndVerify = true; // overlap phases for throughput
});

var provider = services.BuildServiceProvider();
```

## 2. Planning a Migration

Obtain current ("from") and desired ("to") topology snapshots. The planner produces a deterministic ordered list of key moves.

```csharp
var planner = provider.GetRequiredService<IShardMigrationPlanner<string>>();

TopologySnapshot from = /* acquire current snapshot from map store */;
TopologySnapshot to   = /* construct new snapshot after scaling */;

CancellationToken ct = CancellationToken.None;

MigrationPlan plan = await planner.CreatePlanAsync(from, to, ct);
Console.WriteLine($"Plan {plan.PlanId} with {plan.Moves.Count} key moves created.");
```

## 3. Executing the Plan

Resolve the executor from DI, optionally supply a progress observer.

```csharp
var executor = provider.GetRequiredService<ShardMigrationExecutor<string>>();

var progress = new Progress<MigrationProgressEvent>(p =>
{
    Console.WriteLine($"[{p.TimestampUtc:O}] copied={p.Copied} verified={p.Verified} swapped={p.Swapped} failed={p.Failed} activeCopy={p.ActiveCopy} activeVerify={p.ActiveVerify}");
});

MigrationSummary summary = await executor.ExecuteAsync(plan, progress, ct);
Console.WriteLine($"Migration {summary.PlanId} done={summary.Done} failed={summary.Failed} elapsed={summary.Elapsed}.");
```

### Cancellation & Resume

If the process is cancelled, a checkpoint is flushed. Re-running `ExecuteAsync` with the same plan resumes without duplicating work (idempotent transitions). Always persist or reconstruct the same `MigrationPlan` (same `PlanId`) when resuming.

## 4. Metrics

`IShardMigrationMetrics` is resolved via DI (no-op by default). Provide a concrete implementation to export counters/gauges to your telemetry system.

Counters expected:

- Planned
- Copied
- Verified
- Swapped
- Failed
- Retries

Gauges:

- ActiveCopy
- ActiveVerify

Ensure metric increments occur once per key (the executor enforces this before calling the metrics API).

Durations (milliseconds) you can export by extending `IShardMigrationMetrics`:

- Copy duration per key (`ObserveCopyDuration`)
- Verify duration per key (`ObserveVerifyDuration`)
- Swap batch duration (`ObserveSwapBatchDuration`)
- Total elapsed per plan (`ObserveTotalElapsed`)

## 5. Replacing In-Memory Components

For production you should override the default registrations before calling `AddShardisMigration` or by registering your implementations first. An experimental SQL implementation (`Shardis.Migration.Sql`) provides a starting point for durable checkpoint + shard map storage (preview – review source before adopting):

```csharp
services.AddSingleton<IShardDataMover<string>, MyDurableDataMover>();
services.AddSingleton<IShardMigrationCheckpointStore<string>, PostgresCheckpointStore>();
services.AddSingleton<IShardMapSwapper<string>, MyAtomicMapSwapper>();
services.AddSingleton<IVerificationStrategy<string>, HashSampleVerificationStrategy<string>>();
services.AddSingleton<IShardMigrationMetrics, PrometheusMigrationMetrics>();

services.AddShardisMigration<string>(); // keeps existing ones, adds any missing
```

Guidelines:

- Data mover must be idempotent for repeated `CopyAsync` on already-copied keys.
- Checkpoint store must provide atomic replace semantics (no partial writes of state dictionary).
- Swapper must guarantee all-or-nothing per batch; if not natively supported, implement compensation.
- Verification strategy should be deterministic; avoid randomness in sample selection without a fixed seed.

## 6. Configuration Reference

| Option | Default | Description |
| ------ | ------- | ----------- |
| CopyConcurrency | 32 | Parallel copy operations. Higher values increase throughput but pressure source. |
| VerifyConcurrency | 32 | Parallel verification operations; often lower than copy to reduce target read load. |
| SwapBatchSize | 500 | Keys swapped atomically per batch. Larger batches reduce overhead but increase rollback scope. |
| MaxRetries | 5 | Transient retry attempts before marking Failed. |
| RetryBaseDelay | 100 ms | Base delay for exponential backoff (delay * 2^attempt). |
| InterleaveCopyAndVerify | true | Whether to start verifying as soon as keys are copied (overlap phases). |
| EnableDryRunHashSampling | true | Placeholder hook for future partial verification modes. |
| ForceSwapOnVerificationFailure | false | If true, still swaps keys that failed verification (unsafe, for emergency). |
| CheckpointFlushInterval | 2 s | Time-based flush cadence for checkpoint persistence. |
| CheckpointFlushEveryTransitions | 1000 | Transition count threshold triggering a checkpoint flush. |

## 7. Logging & Redaction

Never log full key values. If logging is required, truncate or hash keys (e.g., first 6 bytes of a hash). Shard identifiers may also be redacted if considered sensitive.

## 8. Failure Semantics

- Transient copy/verify errors (network, timeout) ⇒ retried with capped exponential backoff.
- Persistent verification mismatch ⇒ key marked Failed unless `ForceSwapOnVerificationFailure` is set.
- Swap batch failure ⇒ retried; if unrecoverable, batch rolled back (implementation responsibility) and keys remain Copied or mark Failed.

## 9. Benchmarks

See `docs/benchmarks.md` (migration category) for measuring keys/sec under varying concurrency. Use env vars `SHARDIS_FULL` and `SHARDIS_BENCH_MODE` to scale matrix and rigor.

## 10. Cross References

- ADR 0001 (Core Architecture): `docs/adr/0001-core-architecture.md`
- ADR 0002 (Migration Execution): `docs/adr/0002-key-migration-execution.md`

## 11. Next Steps / Extensibility Ideas

- Durable checkpoint store (e.g., PostgreSQL / Redis) with optimistic concurrency.
- Segmented / streaming plan execution for very large keyspaces.
- Adaptive concurrency controller based on observed latencies.
- Additional verification strategies (Bloom filter pre-check, probabilistic sampling with fixed seed).

---

If anything here is unclear or you require an additional adapter sample, open an issue referencing this guide.
