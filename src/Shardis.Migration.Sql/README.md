# Shardis.Migration.Sql

Experimental durable SQL-backed components for Shardis key migration. Provides a checkpoint store and shard map (with history + assignment changed event) to persist migration progress and topology changes.

> Status: Preview. APIs may change; review source before adopting in production.

## Install

```bash
dotnet add package Shardis.Migration.Sql --version 0.2.*
```

## When to use

* Need durable `IShardMigrationCheckpointStore<TKey>` beyond in-memory for long running plans.
* Want persistent shard map + history audit (key -> shard transitions) in SQL.
* Require assignment change event for cache invalidation / downstream listeners.

## What's included

* `SqlCheckpointStore<TKey>` — load/persist migration checkpoints (plan id, per-key state, last processed index, version).
* `SqlShardMapStore<TKey>` — optimistic shard assignment store + history table + `AssignmentChanged` event.
* History recording (`ShardMapHistory`) for audit / replay.

## Schema (baseline)

Minimal tables created (pseudo DDL):

```sql
CREATE TABLE ShardMigrationCheckpoint (
  PlanId TEXT PRIMARY KEY,
  Version INTEGER NOT NULL,
  UpdatedAtUtc TEXT NOT NULL,
  LastProcessedIndex INTEGER NOT NULL,
  StatesJson TEXT NOT NULL
);

CREATE TABLE ShardMap (
  ShardKey TEXT PRIMARY KEY,
  ShardId TEXT NOT NULL
);

CREATE TABLE ShardMapHistory (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  ShardKey TEXT NOT NULL,
  OldShardId TEXT NULL,
  NewShardId TEXT NOT NULL,
  ChangedAtUtc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
```

(Provider-specific types may differ; adapt as needed.)

## Quick start

```csharp
// Register core migration first
a.Services.AddShardisMigration<string>();

// Provide factory creating an open DbConnection (e.g. NpgsqlConnection / SqlConnection)
Func<DbConnection> connectionFactory = () => new NpgsqlConnection(connString);

services.AddSingleton<IShardMigrationCheckpointStore<string>>(sp => new SqlCheckpointStore<string>(connectionFactory));
services.AddSingleton<IShardMapStore<string>>(sp => new SqlShardMapStore<string>(connectionFactory));
```

Subscribe to assignment change notifications:

```csharp
var store = sp.GetRequiredService<IShardMapStore<string>>() as SqlShardMapStore<string>;
store!.AssignmentChanged += (key, oldId, newId) =>
{
    // invalidate cache, publish message, etc.
};
```

## Notes & trade-offs

* Checkpoint `States` persisted as JSON; large plans can grow row size — consider pruning or segmented planner for very large migrations.
* `TryAssignShardToKey` uses optimistic insert semantics; idempotent under concurrent calls.
* History table growth unbounded; implement retention / compaction per ops requirements.
* No automatic index management beyond PKs in this preview.
* Event invocation occurs after successful assignment; avoid heavy work inline.

## Roadmap (planned)

* Batched checkpoint compression.
* Configurable table naming + schema names.
* Retry/backoff policies for transient DB errors.
* Optional soft-delete on shard map rows (tombstoning) for differential analysis.

## Limitations

* No migration of existing in-memory checkpoints.
* No built-in connection resiliency wrapper; wrap factory with Polly if required.
* Not yet load tested for very large (millions) key plans.

## License

MIT (see root repository LICENSE).
