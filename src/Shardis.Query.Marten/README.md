# Shardis.Query.Marten

Marten (PostgreSQL) query provider for Shardis. Supplies a Marten-tuned `IShardQueryExecutor`, helpers, and wiring patterns.

[![NuGet](https://img.shields.io/nuget/v/Shardis.Query.Marten.svg)](https://www.nuget.org/packages/Shardis.Query.Marten/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.Query.Marten.svg)](https://www.nuget.org/packages/Shardis.Query.Marten/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Install

```bash
dotnet add package Shardis.Query.Marten --version 0.1.*
```

## When to use

- Your shard-local persistence is **Marten** and you need a supported query executor.
- You want **streamed** cross-shard results with Marten sessions per shard.
- You prefer minimal glue: DI-friendly setup and sensible defaults.

## What’s included

- `MartenQueryExecutor` — concrete `IShardQueryExecutor<IMartenSessionFactory>`.
- Fluent helpers for page size and batching.
- Sample DI wiring to connect Marten to Shardis’ query API.

## Quick start

```csharp
// minimal usage
var exec = MartenQueryExecutor.Instance.WithPageSize(256);
await foreach (var item in exec.QueryAsync(sessionFactory, myQuery, CancellationToken.None))
{
 // consume streamed items
}
```

## Configuration / Options

- `WithPageSize(int)` to control Marten fetch pages.
- Provide an `IMartenSessionFactory` per shard (typical: tenant-aware factory).

## Integration notes

- Works alongside `Shardis.Query` (core abstractions) and merge operators.
- Typical wiring: register the executor in DI and map shard ids → Marten sessions.

## Capabilities & limits

- ✅ Efficient paging via Marten APIs
- ✅ Compatible with Shardis merge modes (unordered/ordered)
- ⚠️ Ordered streaming requires a deterministic key across shards
- 🧩 TFM: `net8.0`, `net9.0`; Marten compatibility varies by release

## Samples & tests

- Tests: <https://github.com/veggerby/shardis/tree/main/test/Shardis.Query.Marten.Tests>
- Samples: <https://github.com/veggerby/shardis/tree/main/samples>

## Versioning & compatibility

- SemVer; see CHANGELOG: <https://github.com/veggerby/shardis/blob/main/CHANGELOG.md>

## Contributing

- PRs welcome. See <https://github.com/veggerby/shardis/blob/main/CONTRIBUTING.md>

## License

- MIT — see <https://github.com/veggerby/shardis/blob/main/LICENSE>

## Links

- NuGet: <https://www.nuget.org/packages/Shardis.Query.Marten/>
- Issues: <https://github.com/veggerby/shardis/issues>
