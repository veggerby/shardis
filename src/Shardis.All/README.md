# Shardis.All

Complete meta-package for Shardis sharding framework. Includes all packages compatible with .NET 8 and .NET 9.

[![NuGet](https://img.shields.io/nuget/v/Shardis.All.svg)](https://www.nuget.org/packages/Shardis.All/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.All.svg)](https://www.nuget.org/packages/Shardis.All/)

## Install

```bash
dotnet add package Shardis.All
```

## When to use

- ✅ You want the full Shardis experience without cherry-picking individual packages
- ✅ Your application targets .NET 8 or .NET 9
- ✅ You're using or planning to use Marten for document storage
- ✅ You want Redis-backed distributed shard maps
- ❌ Your application requires Entity Framework Core support (use individual packages instead)

## What's included

This meta-package references:

### Core
- `Shardis` – Core routing, hashing, shard map abstractions
- `Shardis.DependencyInjection` – Per-shard resource factories

### Query
- `Shardis.Query` – Query abstractions and merge primitives
- `Shardis.Query.InMemory` – In-memory query executor (testing/prototyping)
- `Shardis.Query.Marten` – Marten query executor with adaptive paging

### Migration
- `Shardis.Migration` – Migration planning and execution primitives
- `Shardis.Migration.Marten` – Marten-based migration provider
- `Shardis.Migration.Sql` – SQL-backed durable migration components (experimental)

### Storage & Integration
- `Shardis.Redis` – Redis shard map store
- `Shardis.Marten` – Marten integration helpers

### Logging
- `Shardis.Logging.Console` – Console logger adapter
- `Shardis.Logging.Microsoft` – Microsoft.Extensions.Logging adapter

## What's NOT included

- **Entity Framework Core packages** (`Shardis.Query.EntityFrameworkCore`, `Shardis.Migration.EntityFrameworkCore`) require .NET 10+ and must be installed separately if needed.

## Quick start

After installing `Shardis.All`, you have access to all core functionality:

```csharp
using Shardis;
using Shardis.DependencyInjection;
using Shardis.Query.Marten;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection()
    .AddShards<IDocumentStore>(2, shard => /* configure Marten store */)
    .AddShardis(opts => opts.UseDefaultRouter())
    .AddMartenQueryExecutor();

await using var provider = services.BuildServiceProvider();

// Start querying across shards...
```

## Configuration

All package-specific configuration options are available. Refer to individual package documentation for details:

- [Shardis (core)](https://github.com/veggerby/shardis/tree/main/src/Shardis)
- [Shardis.Query](https://github.com/veggerby/shardis/tree/main/src/Shardis.Query)
- [Shardis.Migration](https://github.com/veggerby/shardis/tree/main/src/Shardis.Migration)
- [Shardis.Redis](https://github.com/veggerby/shardis/tree/main/src/Shardis.Redis)

## Capabilities & limits

- **Target frameworks**: .NET 8.0, .NET 9.0
- **Compatibility**: All referenced packages share the same version
- **Size**: Meta-package has no additional code; size = sum of included packages

## Samples & tests

See the main [Shardis repository](https://github.com/veggerby/shardis) for samples demonstrating:
- Migration workflows (Marten-based)
- Query execution with adaptive paging
- Health monitoring and resilience
- Redis-backed shard maps

## Versioning & compatibility

- **Version**: Matches the core Shardis version (e.g., `0.3.0`)
- **Breaking changes**: Follow [Shardis versioning policy](https://github.com/veggerby/shardis/blob/main/docs/packaging-and-versioning.md)
- **Support**: .NET 8 LTS and .NET 9 (current)

## Contributing

Contributions welcome! See [CONTRIBUTING.md](https://github.com/veggerby/shardis/blob/main/CONTRIBUTING.md).

## License

MIT – See [LICENSE](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Links

- [Documentation](https://github.com/veggerby/shardis/tree/main/docs)
- [API Reference](https://github.com/veggerby/shardis/blob/main/docs/api.md)
- [Issue Tracker](https://github.com/veggerby/shardis/issues)
- [Discussions](https://github.com/veggerby/shardis/discussions)
