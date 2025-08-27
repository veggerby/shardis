# Shardis.Migration

Key migration execution primitives for Shardis.

## Features

- Migration plan model & execution orchestrator
- Copy / verify / swap phases with retry
- Checkpoint persistence abstraction
- Metrics hooks (planned, copied, verified, swapped, failed, retries)
- In-memory reference implementations

## Usage (Conceptual)

```csharp
var executor = services.GetRequiredService<ShardMigrationExecutor<string>>();
var plan = planner.Plan(sourceShard, targetShard, keys);
var summary = await executor.ExecuteAsync(plan, progress: null, CancellationToken.None);
```

See `docs/migration-usage.md` for full workflow and invariants.
