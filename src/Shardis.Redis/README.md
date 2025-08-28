# Shardis.Redis

Redis-backed map store and helpers for Shardis (connection helpers and sample `RedisShard`).

[![NuGet](https://img.shields.io/nuget/v/Shardis.Redis.svg)](https://www.nuget.org/packages/Shardis.Redis/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.Redis.svg)](https://www.nuget.org/packages/Shardis.Redis/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Install

```bash
dotnet add package Shardis.Redis --version 0.1.*
```

## When to use

- Your shard-local store is Redis and you need a map store or connection helpers.

## What’s included

- `RedisShard` sample implementation and `RedisShardMapStore<T>` helpers.

## Quick start

```csharp
// register a Redis-backed shard map store
services.AddSingleton<IShardMapStore<string>>(sp => new RedisShardMapStore<string>("localhost:6379"));

// create a shard map store directly
var store = new RedisShardMapStore<string>("localhost:6379");
```

## Integration notes

- Depends on StackExchange.Redis; see csproj for version pins.

## Samples & tests

- Samples: [samples](https://github.com/veggerby/shardis/tree/main/samples)
- Tests: [tests](https://github.com/veggerby/shardis/tree/main/test)

## Configuration / Options

- Connection string: provide the Redis endpoint(s) to `RedisShardMapStore<T>`.
- Timeouts and retry policies should be configured in the `ConnectionMultiplexer`.

## Capabilities & limits

- ✅ Provides atomic CAS-style operations for shard map persistence.
- ⚠️ Network partitions and Redis availability affect assignment consistency; use durable checkpointing for migration operations.

## Versioning & compatibility

- SemVer; see CHANGELOG: [CHANGELOG](https://github.com/veggerby/shardis/blob/main/CHANGELOG.md)

## Contributing

- PRs welcome. See [CONTRIBUTING](https://github.com/veggerby/shardis/blob/main/CONTRIBUTING.md)

## Links

- NuGet: [Shardis.Redis on NuGet](https://www.nuget.org/packages/Shardis.Redis/)
- License: [LICENSE](https://github.com/veggerby/shardis/blob/main/LICENSE)
