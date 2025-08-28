# Shardis.Query

Primitives and abstractions for cross-shard query execution in Shardis: streaming enumerators, LINQ helpers, and executor interfaces.

[![NuGet](https://img.shields.io/nuget/v/Shardis.Query.svg)](https://www.nuget.org/packages/Shardis.Query/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.Query.svg)](https://www.nuget.org/packages/Shardis.Query/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Install

```bash
dotnet add package Shardis.Query --version 0.1.*
```

## When to use

- You need streaming cross-shard queries with low memory overhead.
- You want a pluggable executor model across stores (in-memory, EF Core, Marten, …).
- You need merge modes (unordered/ordered) that work with `IAsyncEnumerable<T>`.

## What’s included

- `IShardQueryExecutor<TSession>` — executor interface for shard-local queries.
- Streaming enumerators & merge operators for unordered / ordered streaming.
- LINQ adapter helpers to build shard-friendly query expressions.

## Quick start

```csharp
// resolve a shard query executor and run a query
var exec = provider.GetRequiredService<IShardQueryExecutor<MySession>>();
await foreach (var item in exec.QueryAsync(sessionFactory, myQuery, CancellationToken.None))
{
    // process streamed items
}
```

## Configuration / Options

- Merge modes: unordered (fastest) and ordered (global key selector).
- Backpressure/channel capacity is configurable on the broadcaster/merge layer.

## Integration notes

- Pair with a concrete provider package (e.g., `Shardis.Query.Marten`, EF Core sample).
- Requires a shard session factory and a query object understood by the provider.

## Capabilities & limits

- ✅ Streaming across shards with O(shards + channel capacity) memory
- ✅ Pluggable store providers
- ⚠️ Ordered streaming requires a stable key selector and may add latency
- 🧩 TFM: `net8.0`, `net9.0`; Shardis ≥ 0.1

## Samples & tests

- Samples: <https://github.com/veggerby/shardis/tree/main/samples>
- Tests: <https://github.com/veggerby/shardis/tree/main/test/Shardis.Query.Tests>

## Versioning & compatibility

- SemVer; see CHANGELOG: <https://github.com/veggerby/shardis/blob/main/CHANGELOG.md>

## Contributing

- See <https://github.com/veggerby/shardis/blob/main/CONTRIBUTING.md>

## License

- MIT — see <https://github.com/veggerby/shardis/blob/main/LICENSE>

## Links

- Issues: <https://github.com/veggerby/shardis/issues>
- Discussions: <https://github.com/veggerby/shardis/discussions>
