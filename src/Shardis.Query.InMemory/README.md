# Shardis.Query.InMemory

An in-memory query executor for Shardis used in tests and examples. This package provides a lightweight `IQueryExecutor` implementation that yields deterministic results and is useful in unit tests and local samples.

## When to use

- Use in tests, examples and demos where you need deterministic, fast in-memory shard-local query behavior.

## What the package provides

- `InMemoryQueryExecutor` implementation and small helpers for seeding test data.

## Links

- Tests: `test/Shardis.Query.Tests`

## Quick usage example

```csharp
var exec = new InMemoryQueryExecutor(...); // create with seeded test data
await foreach (var item in exec.QueryAsync(...))
{
    // assert test expectations
}
```
