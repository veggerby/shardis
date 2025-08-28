# Shardis.Migration

Key migration execution primitives for Shardis. This package provides a production-ready executor and reference implementations that make it easy to run deterministic migrations in tests and prototypes.

## When to use

- Use this package to perform controlled key migrations between shard topologies. It is the canonical execution path for migrations; the main `Shardis` package intentionally keeps core routing separate from migration execution.

## What the package provides

- Planner models and helpers (for example `MigrationPlan<TKey>`, `SegmentMove<TKey>`).
- `ShardMigrationExecutor<TKey>` — orchestrates copy → verify → swap phases, persists checkpoints, and emits metrics.
- `ICheckpointStore` abstraction and an `InMemoryCheckpointStore` reference implementation for tests and local runs.
- Helper models: `TopologySnapshot<TKey>`, `KeyMove<TKey>` and execution result types.

## High-level contract

- Input: a `MigrationPlan<TKey>` produced by a planner; the plan describes keys or segments to move and the target topology.
- Execution: the executor applies each step idempotently and persists per-step checkpoints.
- Output: an execution summary containing per-step results and verification status.

## Getting started (recommended)

Install the package from NuGet (package id matches project name) and register the runtime services:

```csharp
// in Startup / Program
services.AddShardisMigration<string>(); // registers planner, executor and the in-memory checkpoint store
```

Create a plan with the planner and execute it:

```csharp
var planner = services.GetRequiredService<IShardMigrationPlanner<string>>();
var from = await planner.CreateTopologySnapshotAsync(..., CancellationToken.None);
var to = await planner.CreateTopologySnapshotAsync(..., CancellationToken.None);
var plan = await planner.CreatePlanAsync(from, to, CancellationToken.None);

var executor = services.GetRequiredService<ShardMigrationExecutor<string>>();
var result = await executor.ExecuteAsync(plan, progress: new ConsoleMigrationProgress(), CancellationToken.None);
if (!result.IsSuccessful)
{
    // inspect result.FailedSegments; retry or escalate
}
```

## Quick usage example

```csharp
// register migration runtime
services.AddShardisMigration<string>();

var planner = provider.GetRequiredService<IShardMigrationPlanner<string>>();
var executor = provider.GetRequiredService<ShardMigrationExecutor<string>>();

var plan = await planner.CreatePlanAsync(fromSnapshot, toSnapshot, CancellationToken.None);
var result = await executor.ExecuteAsync(plan, progress: null, CancellationToken.None);
```

## Examples & patterns

- Long-running migrations: prefer segmented plans (see ADR-0004) and a durable `ICheckpointStore` implementation to bound memory and enable resume.
- Testing: use `InMemoryCheckpointStore` and the reference planner to write deterministic tests. The package includes helpers to inject failures and assert correct resume behavior.

## Public types (high level)

- `ShardMigrationExecutor<TKey>` — primary execution entrypoint.
- `IShardMigrationPlanner<TKey>` — planner abstraction; planners may return `MigrationPlan<TKey>` or stream `SegmentMove<TKey>` for segmented planning.
- `MigrationPlan<TKey>` — model describing the moves and metadata.
- `ICheckpointStore` — persistence contract for durable checkpoints.

## Troubleshooting

- If the executor repeatedly retries the same segment, verify your `ICheckpointStore` semantics and planner idempotency.
- For very large keyspaces, use segmented plans (ADR-0004) to reduce memory usage and permit mid-plan resume.

## Links

- Full migration workflow and invariants: `docs/migration-usage.md`
- ADR: segmented planner & checkpoint ranges: `docs/adr/ADR-0004-segmented-planner.md`
- Samples/tests: `test/Shardis.Migration.Tests`

## Contributing

PRs that add stable, well-tested checkpoint store implementations (SQL, Cosmos DB, Redis, etc.) or that improve the executor's observability are welcome. Follow the project's testing and API documentation conventions.

// (no repo-relative links are present; package README uses API and ADR numbers only)
