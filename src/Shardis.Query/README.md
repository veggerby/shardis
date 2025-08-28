# Shardis.Query

Query execution primitives and abstractions for Shardis: streaming enumerators, LINQ helpers, and executor interfaces.

## When to use

- Use this package when implementing or consuming cross-shard queries and streaming merge operators.

## What the package provides

- `IQueryExecutor` and streaming enumerators.
- LINQ adapter helpers and sample providers (in-memory, EFCore, Marten).

## Quick usage example

```csharp
// resolve a shard query executor and run a query
var exec = provider.GetRequiredService<IShardQueryExecutor<MySession>>();
await foreach (var item in exec.QueryAsync(sessionFactory, myQuery, CancellationToken.None))
{
    // process streamed items
}
```
