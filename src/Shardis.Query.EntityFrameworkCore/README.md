# Shardis.Query.EntityFrameworkCore

Entity Framework Core query executor for Shardis (Where/Select pushdown, unordered streaming, preview ordered buffering, optional failure strategy decoration).

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
- `EfCoreExecutionOptions` (channel capacity, per-shard command timeout, shard concurrency, context lifetime control).
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
    .AddShards<MyDbContext>(2, shard => new MyDbContext(BuildOptionsFor(shard)))
    .AddShardisEfCoreUnordered<MyDbContext>(
        shardCount: 2,
        contextFactory: new EntityFrameworkCoreShardFactory<MyDbContext>(BuildOptionsFor))
    .DecorateShardQueryFailureStrategy(BestEffortFailureStrategy.Instance) // optional
    .AddShardisQueryClient();

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IShardQueryClient>();
var active = await client.Query<Person>().Where(p => p.IsActive).CountAsync();
```

## Samples & tests

- Samples: [samples](https://github.com/veggerby/shardis/tree/main/samples)

## Configuration / Options

- **ChannelCapacity** (`EfCoreExecutionOptions.ChannelCapacity`): bounded backpressure for unordered merge (null = unbounded internal channel).
- **PerShardCommandTimeout**: database command timeout per shard query (best‑effort; ignored if provider disallows).
- **Concurrency**: maximum shard fan‑out (limits simultaneous DbContext queries). Null = unbounded.
- **DisposeContextPerQuery**: if false, retains one `DbContext` per shard for executor lifetime (reduces allocations; ensure thread-safety per context usage pattern).
- **Ordered factory**: `AddShardisEfCoreOrdered` / `EfCoreShardQueryExecutor.CreateOrdered` buffers all shard results before ordering; use only for bounded result sets.
- **Failure strategy decoration**: call `DecorateShardQueryFailureStrategy(BestEffortFailureStrategy.Instance)` (or custom) after registering an executor.

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
