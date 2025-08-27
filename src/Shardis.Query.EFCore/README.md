# Shardis.Query.EFCore

Entity Framework Core query executor for the Shardis fluent query MVP (Where/Select, unordered streaming).

## Features

- Server-side translation of `Where` and final `Select`.
- Unordered streaming merge across shards (arrival order, non-deterministic).
- Automatic `AsNoTracking()` for all queries.
- Optional per-shard command timeout.
- Metrics via `IQueryMetricsObserver` (shard start/stop, items, completion, cancellation).

## Usage

```csharp
using Shardis.Query.Execution.EFCore;
using Shardis.Query;
using Microsoft.EntityFrameworkCore;

var exec = new EfCoreShardQueryExecutor(
  shardCount: 4,
  contextFactory: shardId => CreateDbContext(shardId),
  merge: (streams, ct) => Shardis.Query.Internals.UnorderedMerge.Merge(streams, ct),
  commandTimeoutSeconds: 15 // optional
);

var query = ShardQuery.For<Person>(exec)
                      .Where(p => p.Age >= 30)
                      .Select(p => new { p.Name, p.Age });

await foreach (var row in query)
{
    Console.WriteLine(row.Name);
}
```

Timeout: specify `commandTimeoutSeconds` in the executor constructor; unsupported providers silently ignore it.

Global ordering: wrap shard streams with the ordered merge helper instead of unordered merge (future API exposure).

## Notes

- Ordering across shards is not implemented in this executor; use ordered streaming merge utilities for global ordering.
- Cancellation is cooperative; enumeration stops without throwing unless provider surfaces `OperationCanceledException`.
- Ensure your `DbContext` factory is thread-safe and produces isolated contexts per shard query.
