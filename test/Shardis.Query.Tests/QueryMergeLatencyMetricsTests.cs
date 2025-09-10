using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Shardis.Factories;
using Shardis.Model;
using Shardis.Query.Diagnostics;
using Shardis.Query.EntityFrameworkCore.Execution;
using Shardis.Query.Execution;

namespace Shardis.Query.Tests;

public sealed class QueryMergeLatencyMetricsTests
{
    private sealed class RecordingMetrics : IShardisQueryMetrics
    {
        public int Count;
        public List<(double ms, QueryMetricTags tags)> Records { get; } = new();
        public void RecordQueryMergeLatency(double milliseconds, in QueryMetricTags tags)
        {
            Count++;
            Records.Add((milliseconds, tags));
        }
    }

    private sealed class Person { public int Id { get; set; } public int Age { get; set; } }
    private sealed class PersonContext(DbContextOptions<PersonContext> o) : DbContext(o) { public DbSet<Person> People => Set<Person>(); }

    private static PersonContext Create(int shard, int rows = 2, int delayMs = 0)
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var opt = new DbContextOptionsBuilder<PersonContext>().UseSqlite(conn).Options;
        var ctx = new PersonContext(opt);
        ctx.Database.EnsureCreated();
        if (!ctx.People.Any())
        {
            for (int i = 0; i < rows; i++)
            {
                ctx.People.Add(new Person { Id = shard * 100 + i + 1, Age = 20 + i });
            }
            ctx.SaveChanges();
        }
        if (delayMs > 0) Thread.Sleep(delayMs); // deterministic simple delay (acceptable in unit due to small number)
        return ctx;
    }

    private sealed class Factory(int delayMs) : IShardFactory<DbContext>
    {
        private readonly int _delay = delayMs;
        public async ValueTask<DbContext> CreateAsync(ShardId shardId, CancellationToken ct = default)
        {
            if (_delay > 0) { await Task.Delay(_delay, ct); }
            return Create(int.Parse(shardId.Value));
        }
    }

    [Fact]
    public async Task Metrics_Recorded_On_Success()
    {
        // arrange
        var rec = new RecordingMetrics();
        IShardFactory<DbContext> factory = new Factory(0);
        var exec = new EntityFrameworkCoreShardQueryExecutor(3, factory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), queryMetrics: rec);
        var q = ShardQuery.For<Person>(exec).Where(p => p.Age >= 20);

        // act
        var list = await q.ToListAsync();

        // assert
        rec.Count.Should().Be(1);
        var entry = rec.Records.Single();
        entry.tags.ResultStatus.Should().Be("ok");
        entry.tags.TargetShardCount.Should().Be(3);
        list.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Metrics_Recorded_On_Canceled()
    {
        // arrange
        var rec = new RecordingMetrics();
        IShardFactory<DbContext> factory = new Factory(0);
        var exec = new EntityFrameworkCoreShardQueryExecutor(2, factory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), queryMetrics: rec);
        var q = ShardQuery.For<Person>(exec).Where(p => p.Age >= 20);
        using var cts = new CancellationTokenSource();

        // act
        var task = q.ToListAsync(cts.Token);
        cts.Cancel();
        try { await task; } catch { }

        // assert
        rec.Count.Should().Be(1);
        rec.Records[0].tags.ResultStatus.Should().Be("canceled");
    }

    [Fact]
    public async Task Metrics_Recorded_On_Failure()
    {
        // arrange
        var rec = new RecordingMetrics();
        var factory = new DelegatingShardFactory<DbContext>((sid, ct) =>
        {
            var shard = int.Parse(sid.Value);
            if (shard == 1) throw new InvalidOperationException("boom");
            return new ValueTask<DbContext>(Create(shard));
        });
        var exec = new EntityFrameworkCoreShardQueryExecutor(3, factory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), queryMetrics: rec);
        var q = ShardQuery.For<Person>(exec).Where(p => p.Age >= 20);

        // act
        try
        {
            await q.ToListAsync();
            throw new Exception("Expected failure");
        }
        catch (Exception ex)
        {
            (ex is InvalidOperationException || ex is AggregateException).Should().BeTrue();
        }

        // assert
        rec.Count.Should().Be(1);
        rec.Records[0].tags.ResultStatus.Should().Be("failed");
    }

    [Fact]
    public async Task Metrics_Recorded_On_Ordered_Path()
    {
        // arrange
        var rec = new RecordingMetrics();
        IShardFactory<DbContext> factory = new Factory(0);
        // Build base unordered with metrics sink
        var unordered = new EntityFrameworkCoreShardQueryExecutor(2, factory, (streams, ct) => Internals.UnorderedMerge.Merge(streams, ct), queryMetrics: rec);
        // Use internal helper via reflection to wrap existing unordered so metrics sink is reused
        var helper = typeof(Shardis.Query.EntityFrameworkCore.EfCoreShardQueryExecutor).GetMethod("CreateOrderedFromExisting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        // Build key selector extracting Person.Id from boxed object
        var objParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
        var cast = System.Linq.Expressions.Expression.Convert(objParam, typeof(Person));
        var idProp = System.Linq.Expressions.Expression.Property(cast, nameof(Person.Id));
        var box = System.Linq.Expressions.Expression.Convert(idProp, typeof(object));
        var orderLambda = System.Linq.Expressions.Expression.Lambda<System.Func<object, object>>(box, objParam);
        var orderedExec = (IShardQueryExecutor)helper!.Invoke(null, new object[] { unordered, orderLambda, false })!;

        var q = ShardQuery.For<Person>(orderedExec).Where(p => p.Age >= 20);

        // act
        var list = await q.ToListAsync();

        // assert
        rec.Count.Should().Be(1); // unified single histogram emission
        rec.Records.Single().tags.MergeStrategy.Should().Be("ordered");
        list.Should().NotBeEmpty();
    }
}

internal static class AsyncEnumExtensions
{
    public static async Task ConsumeAsync<T>(this IAsyncEnumerable<T> src)
    {
        await foreach (var _ in src) { }
    }
}