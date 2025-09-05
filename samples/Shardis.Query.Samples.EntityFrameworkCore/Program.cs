using Microsoft.EntityFrameworkCore;

using Shardis.Factories;
using Shardis.Model;
using Shardis.Query;
using Shardis.Query.EntityFrameworkCore.Execution;
using Shardis.Query.EntityFrameworkCore.Factories;
using Shardis.Query.Samples.EntityFrameworkCore;

static DbContextOptions<PersonContext> CreateOptions(int shardId)
{
    var builder = new DbContextOptionsBuilder<PersonContext>();
    builder.UseSqlite($"Data Source=person-shard-{shardId}.db");
    return builder.Options;
}

var shardCount = 2;
var personFactory = new EntityFrameworkCoreShardFactory<PersonContext>(sid =>
{
    var id = int.Parse(sid.Value);
    return CreateOptions(id);
});

// Seed data once per shard (outside factory to keep factory pure)
for (var i = 0; i < shardCount; i++)
{
    var sid = new ShardId(i.ToString());
    await using var ctx = await personFactory.CreateAsync(sid);
    Seed.Ensure(ctx, i == 0
        ? new[] { new Person { Id = 1, Name = "Alice", Age = 35 }, new Person { Id = 2, Name = "Bob", Age = 28 } }
        : [new Person { Id = 3, Name = "Carol", Age = 42 }, new Person { Id = 4, Name = "Dave", Age = 31 }]);
}

// Adapter exposing DbContext (non-generic) factory for executor
IShardFactory<DbContext> adapterFactory = new DelegatingShardFactory<DbContext>(async (sid, ct) => await personFactory.CreateAsync(sid, ct));

var exec = new EntityFrameworkCoreShardQueryExecutor(
    shardCount: shardCount,
    contextFactory: adapterFactory,
    merge: (streams, ct) => UnorderedMergeHelper.Merge(streams, ct));

var q = ShardQuery.For<Person>(exec)
                    .Where(p => p.Age >= 30)
                    .Select(p => new { p.Name, p.Age });

await foreach (var row in q)
{
    Console.WriteLine($"{row.Name} ({row.Age})");
}