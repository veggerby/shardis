# Shardis.Query.EntityFrameworkCore

Entity Framework Core query executor for Shardis (Where/Select pushdown, unordered streaming, preview ordered buffering).

[![NuGet](https://img.shields.io/nuget/v/Shardis.Query.EntityFrameworkCore.svg)](https://www.nuget.org/packages/Shardis.Query.EntityFrameworkCore/)
[![Downloads](https://img.shields.io/nuget/dt/Shardis.Query.EntityFrameworkCore.svg)](https://www.nuget.org/packages/Shardis.Query.EntityFrameworkCore/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Install

```bash
dotnet add package Shardis.Query.EntityFrameworkCore --version 0.1.*
```

## When to use

- Your shard-local persistence is EF Core and you want query pushdown and streaming.
- You need a tested executor that integrates with `DbContext` per shard.

## What’s included

- `EntityFrameworkCoreShardQueryExecutor` — concrete executor translating queries into EF Core operations.
- `EfCoreShardQueryExecutor.CreateUnordered` and `CreateOrdered` (buffered ordered variant; materializes then orders).
- `EfCoreExecutionOptions` (channel capacity, per-shard command timeout, future concurrency hints).
- `EntityFrameworkCoreShardFactory<TContext>` / `PooledEntityFrameworkCoreShardFactory<TContext>` for per-shard context creation.
- Wiring examples for registering `DbContext` instances per shard.

## Quick start

```csharp
// Build a shard factory (pure creation only)
var dbFactory = new EntityFrameworkCoreShardFactory<MyDbContext>(sid =>
    new DbContextOptionsBuilder<MyDbContext>()
        .UseSqlite($"Data Source=shard-{sid.Value}.db")
        .Options);

// (Optional) seed outside the factory
foreach (var sid in new[]{ new ShardId("0"), new ShardId("1") })
{
    await using var ctx = await dbFactory.CreateAsync(sid);
    // seed if empty
}

// Adapter to non-generic DbContext for executor
IShardFactory<DbContext> adapter = new DelegatingShardFactory<DbContext>((sid, ct) => new ValueTask<DbContext>(dbFactory.Create(sid)));

var exec = new EntityFrameworkCoreShardQueryExecutor(
    shardCount: 2,
    contextFactory: adapter,
    merge: (streams, ct) => UnorderedMerge.Merge(streams, ct));

var query = ShardQuery.For<Person>(exec)
    .Where(p => p.Age >= 30)
    .Select(p => p.Name);

var names = await query.ToListAsync();
```

## Integration notes

- Works with `Shardis.Query` core abstractions; register per-shard `DbContext` factories in DI.
- Recommended DI approach using `Shardis.DependencyInjection`:

```csharp
var services = new ServiceCollection()
    .AddShards<MyDbContext>(2, shard => new MyDbContext(BuildOptionsFor(shard)));

await using var provider = services.BuildServiceProvider();
var perShardFactory = provider.GetRequiredService<IShardFactory<MyDbContext>>();
IShardFactory<DbContext> adapter = new DelegatingShardFactory<DbContext>((sid, ct) => perShardFactory.CreateAsync(sid, ct));
var exec = new EntityFrameworkCoreShardQueryExecutor(2, adapter, (s, ct) => UnorderedMerge.Merge(s, ct));
```

## Samples & tests

- Samples: [samples](https://github.com/veggerby/shardis/tree/main/samples)

## Configuration / Options

- **ChannelCapacity** (`EfCoreExecutionOptions.ChannelCapacity`): bounded backpressure for unordered merge (null = provider default / unbounded internal channel).
- **PerShardCommandTimeout**: database command timeout applied to per-shard queries (mapped to underlying EF Core command timeout where possible).
- **Concurrency** (reserved): future hint for shard fan-out parallelism.
- **DisposeContextPerQuery**: if false, caller manages DbContext lifetime (default true).
- **Ordered factory**: `CreateOrdered` buffers all shard results before ordering; use only for bounded result sets. Future streaming ordered variant will reduce memory usage.

## Capabilities & limits

- ✅ Pushes where/select operations to EF Core where supported.
- ⚠️ Ordered (buffered) factory materializes all results. Avoid for unbounded / very large sets.
- ⚠️ Ordered streaming improvements (k-way merge) planned; present variant trades memory for simplicity.
- 🧩 Requires EF Core provider matching your database version.

## Versioning & compatibility

- SemVer; see CHANGELOG: [CHANGELOG](https://github.com/veggerby/shardis/blob/main/CHANGELOG.md)

## Contributing

- PRs welcome. See [CONTRIBUTING](https://github.com/veggerby/shardis/blob/main/CONTRIBUTING.md)

## License

- MIT — see [LICENSE](https://github.com/veggerby/shardis/blob/main/LICENSE)

## Links

- NuGet: [Shardis.Query.EntityFrameworkCore on NuGet](https://www.nuget.org/packages/Shardis.Query.EntityFrameworkCore/)
