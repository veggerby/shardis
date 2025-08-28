# Shardis.Query.EFCore

Entity Framework Core query executor for Shardis (Where/Select pushdown, unordered streaming).

[![NuGet](https://img.shields.io/nuget/v/Shardis.Query.EFCore.svg)](https://www.nuget.org/packages/Shardis.Query.EFCore/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.Query.EFCore.svg)](https://www.nuget.org/packages/Shardis.Query.EFCore/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Install

```bash
dotnet add package Shardis.Query.EFCore --version 0.1.*
```

## When to use

- Your shard-local persistence is EF Core and you want query pushdown and streaming.
- You need a tested executor that integrates with `DbContext` per shard.

## What’s included

- `EfCoreQueryExecutor` — concrete executor that translates queries into EF Core operations.
- Wiring examples for registering `DbContext` instances per shard.

## Quick start

```csharp
var exec = new EfCoreShardQueryExecutor(shardCount, shardId => new MyDbContext(...), mergeFunc);
await foreach (var item in exec.QueryAsync(...))
{
 // consume streamed items
}
```

## Integration notes

- Works with `Shardis.Query` core abstractions; register per-shard `DbContext` factories in DI.

## Samples & tests

- Samples: [samples](https://github.com/veggerby/shardis/tree/main/samples)

## Configuration / Options

- **PageSize**: control the EF Core query page size for paged streaming (provider-specific).
- **DbContext factory**: provide a per-shard `DbContext` factory in DI; prefer scoped lifetimes.

## Capabilities & limits

- ✅ Pushes where/select operations to EF Core where supported.
- ⚠️ Ordered streaming can add latency and requires a stable key selector across shards.
- 🧩 Requires EF Core provider matching your database version.

## Versioning & compatibility

- SemVer; see CHANGELOG: [CHANGELOG](https://github.com/veggerby/shardis/blob/main/CHANGELOG.md)

## Contributing

- PRs welcome. See [CONTRIBUTING](https://github.com/veggerby/shardis/blob/main/CONTRIBUTING.md)

## License

- MIT — see [LICENSE](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Links

- NuGet: [Shardis.Query.EFCore on NuGet](https://www.nuget.org/packages/Shardis.Query.EFCore/)
