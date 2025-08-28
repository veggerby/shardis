# Shardis.Query.Marten

Marten (Postgres) query provider for Shardis. Provides a query executor and helpers tuned for Marten's APIs and document model patterns.

## When to use

- Use when your shard-local persistence uses Marten and you want a supported query executor.

## What the package provides

- `MartenQueryExecutor` and sample wiring to integrate Marten with the Shardis query API.

## Quick usage example

```csharp
var exec = MartenQueryExecutor.Instance.WithPageSize(256);
await foreach (var item in exec.QueryAsync(sessionFactory, myQuery, CancellationToken.None)) { }
```

## Links

- Samples/tests: `test/Shardis.Query.Marten.Tests` (if present) and `Shardis.Query.Samples.EFCore` for cross-reference
