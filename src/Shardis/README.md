# Shardis

Deterministic sharding primitives for .NET: routing, hashing, and core DI extension points used across the Shardis ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Shardis.svg)](https://www.nuget.org/packages/Shardis/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.svg)](https://www.nuget.org/packages/Shardis/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Install

```bash
dotnet add package Shardis --version 0.1.*
```

## When to use

- You need deterministic shard routing and stable shard identifier types.
- You want pluggable hashing and routing strategies via DI.
- You need low-level primitives for building higher-level shard-aware components.

## What’s included

- `Shard`, `ShardId`, `ShardKey` — core value types.
- `IShardRouter<T>` and `DefaultShardRouter` — routing abstractions and a default implementation.
- `IShardFactory<T>` — provider-agnostic factory for shard-scoped resources (DbContext, Marten sessions, Redis databases, etc.).
- `IShardMap` & `InMemoryShardMap` — declarative shard configuration / connection source.
- Hashing abstractions: `IShardKeyHasher<TKey>`, `IShardRingHasher`.
- `ServiceCollectionExtensions.AddShardis()` — DI wiring helpers.

## Quick start

```csharp
// register core sharding primitives
services.AddShardis<string>(opts =>
{
    opts.ReplicationFactor = 3;
});

// resolve router + map store
var router = provider.GetRequiredService<IShardRouter<string>>();
var mapStore = provider.GetRequiredService<IShardMapStore<string>>();
```

## Configuration / Options

- `ServiceCollectionExtensions.AddShardis<TK>(options => {...})` — configure replication factor and defaults.

## Integration notes

- Works as the core package for other `Shardis.*` packages (queries, persistence providers, migration).
- Link to repository: <https://github.com/veggerby/shardis>

## Capabilities & limits

- ✅ Deterministic routing primitives and small, composable abstractions.
- ⚠️ This package exposes core primitives only — concrete providers live in other packages.
- 🧩 TFM: `net8.0`, `net9.0`.

## Samples & tests

- Samples: <https://github.com/veggerby/shardis/tree/main/samples>
- Tests: <https://github.com/veggerby/shardis/tree/main/test/Shardis.Tests>

## Versioning & compatibility

- SemVer; see CHANGELOG: <https://github.com/veggerby/shardis/blob/main/CHANGELOG.md>

## Contributing

Please read contribution guidelines: <https://github.com/veggerby/shardis/blob/main/CONTRIBUTING.md>

## License

MIT — see <https://github.com/veggerby/shardis/blob/main/LICENSE>

## Links

- Issues: <https://github.com/veggerby/shardis/issues>
- Discussions: <https://github.com/veggerby/shardis/discussions>
