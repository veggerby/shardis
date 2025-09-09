# Shardis.Migration.EntityFrameworkCore

Entity Framework Core migration provider for Shardis. Provides per-key copy (upsert), verification (rowversion or checksum), and projection extensibility for shard key migrations.

## Install

```bash
dotnet add package Shardis.Migration.EntityFrameworkCore --version 0.2.*
```

## When to use

* Relational shards accessed via EF Core.
* Need rowversion or content checksum verification.
* Deterministic, restartable copy → verify → swap execution.

## What's included

* `EntityFrameworkCoreDataMover<TKey,TContext,TEntity>` — per-key copy + upsert.
* `RowVersionVerificationStrategy<TKey,TContext,TEntity>` — compares `rowversion`/`timestamp` bytes.
* `ChecksumVerificationStrategy<TKey,TContext,TEntity>` — canonical JSON + stable hash (see `docs/canonicalization.md`).
* DI extensions: `AddEntityFrameworkCoreMigrationSupport<TKey,TContext,TEntity>()`, `AddEntityFrameworkCoreChecksumVerification<TKey,TContext,TEntity>()`.

## Quick start

```csharp
services.AddShardisMigration<Guid>()
        .AddEntityFrameworkCoreMigrationSupport<Guid, MyShardDbContext, MyEntity>();

// Switch strategy
services.AddEntityFrameworkCoreChecksumVerification<Guid, MyShardDbContext, MyEntity>();
```

Execute:

```csharp
var planner = sp.GetRequiredService<IShardMigrationPlanner<Guid>>();
var executor = sp.GetRequiredService<ShardMigrationExecutor<Guid>>();
var plan = await planner.CreatePlanAsync(fromTopology, toTopology, CancellationToken.None);
var summary = await executor.ExecuteAsync(plan, null, CancellationToken.None);
```

## Notes

* Provide durable checkpoint store for large plans.
* Register custom `IEntityProjectionStrategy` to drop volatile columns before checksum.
* See `samples/Shardis.Migration.Sample` for scenarios.
