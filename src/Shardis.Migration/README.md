# Shardis.Migration

Key migration execution primitives for Shardis. This package contains the executor, planner abstractions, checkpoint store contracts, and reference in-memory implementations used for tests and prototypes.

[![NuGet](https://img.shields.io/nuget/v/Shardis.Migration.svg)](https://www.nuget.org/packages/Shardis.Migration/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.Migration.svg)](https://www.nuget.org/packages/Shardis.Migration/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Install

```bash
dotnet add package Shardis.Migration --version 0.1.*
```

## When to use

- You need a deterministic, idempotent migration executor for moving keys between topologies.
- You want reference `ICheckpointStore` implementations for tests and local runs.

## What’s included

- Planner models: `MigrationPlan<TKey>`, `SegmentMove<TKey>` and related helpers.
- `ShardMigrationExecutor<TKey>` — orchestrates copy → verify → swap with checkpoints and metrics hooks.
- `ICheckpointStore` and `InMemoryCheckpointStore` reference implementation.

## Quick start

```csharp
// register migration runtime
services.AddShardisMigration<string>();

var planner = services.GetRequiredService<IShardMigrationPlanner<string>>();
var executor = services.GetRequiredService<ShardMigrationExecutor<string>>();

var plan = await planner.CreatePlanAsync(fromSnapshot, toSnapshot, CancellationToken.None);
var result = await executor.ExecuteAsync(plan, progress: null, CancellationToken.None);
```

## Integration notes

- For long-running migrations prefer segmented plans and a durable `ICheckpointStore` (SQL/Cosmos/Redis) to bound memory and enable resume.

## Samples & tests

- Docs: <https://github.com/veggerby/shardis/blob/main/docs/migration-usage.md>
- ADR: <https://github.com/veggerby/shardis/blob/main/docs/adr/0004-segmented-planner.md>
- Tests: <https://github.com/veggerby/shardis/tree/main/test/Shardis.Migration.Tests>

## Contributing

- PRs that add durable checkpoint store implementations or improve executor observability are welcome. See <https://github.com/veggerby/shardis/blob/main/CONTRIBUTING.md>

## License

- MIT — see <https://github.com/veggerby/shardis/blob/main/LICENSE>
