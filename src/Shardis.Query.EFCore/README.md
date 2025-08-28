# Shardis.Query.EFCore

EF Core integration for Shardis query execution. This package contains a query executor that translates Shardis query calls into EF Core queries against a relational database.

## When to use

- Use when your shard-local stores are relational databases accessed via EF Core and you want a pre-built executor.

## What the package provides

- `EfCoreQueryExecutor` and wiring examples for registering EF Core DbContexts per shard.

## Quick usage example

```csharp
var exec = new EfCoreShardQueryExecutor(shardCount, shardId => new MyDbContext(...), mergeFunc);
await foreach (var item in exec.QueryAsync(...)) { }
```

## Links

- Samples: `Shardis.Query.Samples.EFCore` for a runnable example
