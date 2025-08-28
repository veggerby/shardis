# Shardis.Marten

Marten integration for Shardis: per-shard sessions, query executors, and helpers tuned to Marten's APIs.

[![NuGet](https://img.shields.io/nuget/v/Shardis.Marten.svg)](https://www.nuget.org/packages/Shardis.Marten/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.Marten.svg)](https://www.nuget.org/packages/Shardis.Marten/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Install

```bash
dotnet add package Shardis.Marten --version 0.1.*
```

## When to use

- Your shard-local persistence is Marten/Postgres and you want a supported executor.
- You want per-shard Marten sessions and helpers for paging.

## What’s included

- `MartenQueryExecutor` and `MartenShard` helpers.
- DI wiring patterns and page-size helpers tuned for Marten.

## Quick start

```csharp
// use the Marten query executor singleton
var exec = MartenQueryExecutor.Instance.WithPageSize(256);
await foreach (var item in exec.QueryAsync(sessionFactory, myQuery, CancellationToken.None))
{
 // consume streamed items
}
```

## Integration notes

- Works with `Shardis.Query` core abstractions and the Marten query provider package.

## Samples & tests

- Samples: [samples](https://github.com/veggerby/shardis/tree/main/samples)
- Tests: [Shardis.Query.Marten.Tests](https://github.com/veggerby/shardis/tree/main/test/Shardis.Query.Marten.Tests)

## Configuration / Options

- `WithPageSize(int)` — tune Marten fetch page size to balance latency and memory.
- Provide an `IMartenSessionFactory` per shard; prefer pooled session factories for throughput.

## Capabilities & limits

- ✅ Efficient paging and document-oriented queries via Marten.
- ⚠️ Requires compatible Marten/Postgres versions; check package references.

## Versioning & compatibility

- SemVer; see CHANGELOG: [CHANGELOG](https://github.com/veggerby/shardis/blob/main/CHANGELOG.md)

## Contributing

- PRs welcome. See [CONTRIBUTING](https://github.com/veggerby/shardis/blob/main/CONTRIBUTING.md)

## Links

- NuGet: [Shardis.Marten on NuGet](https://www.nuget.org/packages/Shardis.Marten/)
- Docs/samples: [samples](https://github.com/veggerby/shardis/tree/main/samples)

## License

- MIT — see [LICENSE](https://github.com/veggerby/shardis/blob/main/LICENSE)
