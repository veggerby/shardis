# Shardis Core

Core primitives for the Shardis sharding framework.

## Features

- Consistent hashing & default modulo routing
- Virtual node replication (configurable factor, max 10,000)
- Pluggable key and ring hashers
- Shard map stores (in-memory, external via extension packages)
- Atomic compare-and-set assignment (`TryAssignShardToKey`, `TryGetOrAdd`)
- Broadcasting & async merge/ordered enumerators
- Metrics abstraction (single-miss invariant)
- Migration scaffolding

## Installation

```bash
dotnet add package Shardis
```

## Quick Start

```csharp
services.AddShardis<MyShard, string, Session>(o =>
{
    o.Shards.Add(new MyShard("shard-a"));
    o.Shards.Add(new MyShard("shard-b"));
});
```

Resolve a shard for a key:

```csharp
var router = provider.GetRequiredService<IShardRouter<string, Session>>();
var shard = router.RouteToShard(new ShardKey<string>("user-123"));
```

## Documentation

Full docs: [https://github.com/veggerby/shardis](https://github.com/veggerby/shardis)

## License

MIT
