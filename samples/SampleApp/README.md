# Shardis Basic Routing Sample (Console)

Demonstrates core shard routing using the default router and an in-memory shard map.

## What it shows

* Defining simple shards (`SimpleShard`)
* Routing string shard keys to shards via `DefaultShardRouter`
* Repeated routing determinism (same key -> same shard)
* Simulated per-shard session usage

## Running

```bash
dotnet run --project samples/SampleApp
```

## Notes

* Connection/session strings are placeholders; replace with real connection info in real usage.
* `DefaultShardRouter` uses a hashing strategy to map keys deterministically.
