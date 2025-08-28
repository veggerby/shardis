# Shardis

Core sharding abstractions and utilities. This package contains the routing, hashing and core public types that other Shardis packages depend on.

## When to use

- Reference this package when you need deterministic routing, shard identifier types and DI extension points for pluggable hashing and routing strategies.

## What the package provides

- Core models: `Shard`, `ShardId`, `ShardKey`.
- Router abstractions and default implementations: `IShardRouter`, `DefaultShardRouter`.
- Hashing abstractions: `IShardKeyHasher<TKey>`, `IShardRingHasher`.
- DI extension: `ServiceCollectionExtensions.AddShardis()` to wire defaults.

## Quick usage example

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
