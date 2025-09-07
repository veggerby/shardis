using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Shardis.Factories;
using Shardis.Query.EntityFrameworkCore.Execution;

namespace Shardis.Query.Tests;

public sealed class EntityFrameworkCoreMetricsTests
{
    [Fact]
    public async Task Metrics_Observer_ReceivesLifecycle_EntityFrameworkCore()
    {
        // arrange
        var obs = new RecordingObserver();
        IShardFactory<DbContext> factory = new DelegatingShardFactory<DbContext>((sid, ct) => new ValueTask<DbContext>(Create(int.Parse(sid.Value))));
        var exec = new EntityFrameworkCoreShardQueryExecutor(2, factory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), obs);
        var q = ShardQuery.For<Person>(exec).Where(p => p.Age > 20);

        // act
        var list = await q.ToListAsync();

        // assert
        list.Should().NotBeEmpty();
        obs.ShardStarts.Should().Be(2);
        obs.ShardStops.Should().Be(2);
        obs.Completed.Should().BeTrue();
        obs.Canceled.Should().BeFalse();
        obs.ItemsProduced.Should().BeGreaterThan(0);
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
        public int ShardStarts; public int ShardStops; public int ItemsProduced; public bool Completed; public bool Canceled;
        public void OnShardStart(int shardId) => Interlocked.Increment(ref ShardStarts);
        public void OnItemsProduced(int shardId, int count) => Interlocked.Add(ref ItemsProduced, count);
        public void OnShardStop(int shardId) => Interlocked.Increment(ref ShardStops);
        public void OnCompleted() => Completed = true;
        public void OnCanceled() => Canceled = true;
    }
}