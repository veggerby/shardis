# Shardis.Marten

Marten (Postgres) integration for Shardis: query executor and helpers for using Marten as the shard-local persistence provider.

## When to use

- Use this package when your application persists domain objects in Marten/Postgres and you want a ready-made QueryExecutor that speaks Marten's APIs.

## What the package provides

- `MartenQueryExecutor` and `MartenShard` conveniences.
- Examples showing how to wire Marten query execution into Shardis queries.

## Links

- Samples: `Shardis.Query.Samples.EFCore` and `SampleApp` for examples

## Quick usage example

```csharp
// use the Marten query executor singleton
var exec = MartenQueryExecutor.Instance.WithPageSize(256);
var results = await exec.ExecuteAsync(session, query, CancellationToken.None);
```
