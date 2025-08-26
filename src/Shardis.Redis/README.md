# Shardis.Redis

Redis-backed shard map store for Shardis.

## Features

- Redis key/value persistence for shard assignments
- Atomic first-assignment (`SET NX`) semantics
- `TryGetOrAdd` fast path

## Installation

```bash
dotnet add package Shardis.Redis
```

## Usage

```csharp
services.AddSingleton<IShardMapStore<string>>(_ => new RedisShardMapStore<string>("localhost:6379"));
services.AddShardis<MyShard, string, Session>(o =>
{
    o.Shards.Add(new MyShard("shard-a"));
    o.Shards.Add(new MyShard("shard-b"));
});
```

## Notes

Keys are stored under `shardmap:<key>` with the shard id as value.

## License

MIT
