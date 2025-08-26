# Shardis.Marten

Marten integration helpers for Shardis (query execution / shard session support).

## Installation

```bash
dotnet add package Shardis.Marten
```

## Usage

```csharp
// Register shards that each encapsulate a Marten session or document store wrapper.
services.AddShardis<MartenShard, Guid, IDocumentSession>(o =>
{
    o.Shards.Add(new MartenShard("shard-a", storeA));
    o.Shards.Add(new MartenShard("shard-b", storeB));
});
```

## License

MIT
