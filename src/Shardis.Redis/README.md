# Shardis.Redis

Redis integrations for Shardis: helpers for mapping shards to Redis instances and a small `RedisShard` implementation sample.

## When to use

- Use when you store shard-local data in Redis and need helpers to manage shard connections and topology mapping.

## What the package provides

- `RedisShard` sample implementation and connection helpers.
- Integration points for checkpoint stores or distributed coordination backed by Redis (examples/reserved API surface).

## Links

- Samples: `samples/` and `Shardis.Redis` tests

## Quick usage example

```csharp
// register a Redis-backed shard map store
services.AddSingleton<IShardMapStore<string>>(sp => new RedisShardMapStore<string>("localhost:6379"));

// create a shard map store directly
var store = new RedisShardMapStore<string>("localhost:6379");
```
