using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Shardis.Factories;
using Shardis.Query.EntityFrameworkCore.Execution;

namespace Shardis.Query.Tests;

public sealed class EntityFrameworkCoreTargetingInvalidShardTests
{
    [Fact]
    public async Task Targeting_Ignores_InvalidShardIds()
    {
        // arrange
        var obs = new RecordingObserver();
        IShardFactory<DbContext> factory = new DelegatingShardFactory<DbContext>((sid, ct) => new ValueTask<DbContext>(Create(int.Parse(sid.Value))));
        var exec = new EntityFrameworkCoreShardQueryExecutor(3, factory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), obs);
        var all = ShardQuery.For<Person>(exec);

        // act
        var targeted = await all.WhereShard(new Shardis.Model.ShardId("0"), new Shardis.Model.ShardId("99"), new Shardis.Model.ShardId("-1"), new Shardis.Model.ShardId("2")).ToListAsync();

        // assert
        // valid shards: 0 and 2. Each has two people, one age 30 and one 10
        targeted.Should().HaveCount(4);
        targeted.Select(p => p.Id / 10).Distinct().OrderBy(x => x).Should().BeEquivalentTo(new[] { 0, 2 });
    }

    private static PersonContext Create(int shard)
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var opt = new DbContextOptionsBuilder<PersonContext>().UseSqlite(conn).Options;
        var ctx = new PersonContext(opt);
        ctx.Database.EnsureCreated();
        if (!ctx.People.Any())
        {
            ctx.People.AddRange(
                new Person { Id = shard * 10 + 1, Age = 30 },
                new Person { Id = shard * 10 + 2, Age = 10 });
            ctx.SaveChanges();
        }
        return ctx;
    }

    private sealed class Person { public int Id { get; set; } public int Age { get; set; } }
    private sealed class PersonContext(DbContextOptions<PersonContext> o) : DbContext(o) { public DbSet<Person> People => Set<Person>(); }

    private sealed class RecordingObserver : Diagnostics.IQueryMetricsObserver
    {
        public void OnShardStart(int shardId) { }
        public void OnItemsProduced(int shardId, int count) { }
        public void OnShardStop(int shardId) { }
        public void OnCompleted() { }
        public void OnCanceled() { }
    }
}