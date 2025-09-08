# Shardis.Migration

Key migration execution primitives for Shardis. This package contains the executor, planner abstractions, checkpoint store contracts, and reference in-memory implementations used for tests and prototypes.

[![NuGet](https://img.shields.io/nuget/v/Shardis.Migration.svg)](https://www.nuget.org/packages/Shardis.Migration/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.Migration.svg)](https://www.nuget.org/packages/Shardis.Migration/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Install

```bash
dotnet add package Shardis.Migration --version 0.2.*
```

## When to use

- Deterministic, idempotent key migration (copy → verify → swap) between shard topologies.
- Need pluggable verification (checksum, rowversion via provider) & projection abstractions.
- Require checkpointing & resume support during long-running migrations.

## What’s included

- Planner models: `MigrationPlan<TKey>`, `KeyMove<TKey>`, `MigrationCheckpoint<TKey>`.
- `ShardMigrationExecutor<TKey>` — orchestrates copy → verify → swap with checkpoint persistence & metrics hooks.
- Abstractions: `IShardDataMover<TKey>`, `IVerificationStrategy<TKey>`, `IShardMigrationCheckpointStore<TKey>`, `IShardMapSwapper<TKey>`, `IShardMigrationMetrics`.
- Reference in-memory checkpoint store & test doubles (see tests) for prototyping.
- Default stable canonicalization + hashing abstractions (`IStableCanonicalizer`, `IStableHasher`) consumed by checksum strategies. See `docs/canonicalization.md`.

## Quick start

```csharp
// register migration runtime + (optionally) provider-specific support (e.g. EF / Marten)
services.AddShardisMigration<string>(); // core abstractions & executor

var planner = services.GetRequiredService<IShardMigrationPlanner<string>>(); // if planner registered
var executor = services.GetRequiredService<ShardMigrationExecutor<string>>();

var plan = await planner.CreatePlanAsync(fromSnapshot, toSnapshot, CancellationToken.None);
var result = await executor.ExecuteAsync(plan, progress: null, CancellationToken.None);
```

## Integration notes

- For long migrations use durable checkpoint store implementation (custom) rather than in-memory.
- Verification strategy can be swapped (rowversion, checksum) by provider DI extensions before execution.
- Projection strategy (`IEntityProjectionStrategy`) allows shape normalization (exclude volatile fields) — ensure determinism.

## Samples & tests

- Docs: <https://github.com/veggerby/shardis/blob/main/docs/migration-usage.md>
- Canonicalization: <https://github.com/veggerby/shardis/blob/main/docs/canonicalization.md>
- ADR: <https://github.com/veggerby/shardis/blob/main/docs/adr/0004-segmented-planner.md>
- Tests: <https://github.com/veggerby/shardis/tree/main/test/Shardis.Migration.Tests>

## Contributing

- PRs that add durable checkpoint store implementations or improve executor observability are welcome. See <https://github.com/veggerby/shardis/blob/main/CONTRIBUTING.md>

## License

- MIT — see <https://github.com/veggerby/shardis/blob/main/LICENSE>
