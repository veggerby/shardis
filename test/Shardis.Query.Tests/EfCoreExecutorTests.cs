using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Shardis.Query.Tests;

public sealed class EfCoreExecutorTests
{
    private sealed class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }
    private sealed class PersonContext : DbContext
    {
        public PersonContext(DbContextOptions<PersonContext> options) : base(options) { }
        public DbSet<Person> People => Set<Person>();
    }

    [Fact]
    public async Task EfCoreExecutor_ServerSideWhereSelect()
    {
        var exec = new Shardis.Query.Execution.EFCore.EfCoreShardQueryExecutor(2, shardId => CreateAndSeedSqlite(shardId), UnorderedConcurrentMerge);
        var q = ShardQuery.For<Person>(exec).Where(p => p.Age >= 30).Select(p => new { p.Name, p.Age });
        var results = await q.ToListAsync();
        results.Should().BeEquivalentTo(new[] { new { Name = "Alice", Age = 35 }, new { Name = "Carol", Age = 40 } });
    }

    [Fact]
    public async Task EfCoreExecutor_Streaming_FirstItemBeforeSlowShardCompletes()
    {
        var delayMs = 150; // simulate slow shard 1
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var exec = new Shardis.Query.Execution.EFCore.EfCoreShardQueryExecutor(2, shardId => CreateAndSeedSqlite(shardId), (streams, ct) => SlowSecondShardMerge(streams, ct, delayMs));
        var q = ShardQuery.For<Person>(exec).Where(p => p.Age >= 25).Select(p => p.Name);
        var collected = new List<string>();
        await foreach (var name in q)
        {
            collected.Add(name);
            if (collected.Count == 1) { break; }
        }
        sw.Stop();
        collected.Should().NotBeEmpty();
        sw.ElapsedMilliseconds.Should().BeLessThan(delayMs); // first item arrived before slow shard delay elapsed
    }

    [Fact]
    public async Task EfCore_NoClientEvaluation()
    {
        var exec = new Shardis.Query.Execution.EFCore.EfCoreShardQueryExecutor(1, _ => CreateAndSeedSqlite(0, configureWarnings: true), UnorderedConcurrentMerge);
        // Non-translatable predicate -> should throw translation exception (QueryTranslationFailed) instead of client evaluating
        var q = ShardQuery.For<Person>(exec).Where(p => StringHelper.ReverseStringStatic(p.Name) == "Alice");
        var act = async () => await q.ToListAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static PersonContext CreateAndSeedSqlite(int shardId, bool configureWarnings = false)
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<PersonContext>()
            .UseSqlite(conn)
            .EnableSensitiveDataLogging()
            .ConfigureWarnings(w => { /* EF version lacks specific QueryTranslationFailed id or not exposed here */ })
            .Options;
        var ctx = new PersonContext(options);
        ctx.Database.EnsureCreated();
        if (!ctx.People.Any())
        {
            if (shardId == 0)
            {
                ctx.People.AddRange(new Person { Id = 1, Name = "Alice", Age = 35 }, new Person { Id = 2, Name = "Bob", Age = 25 });
            }
            else
            {
                ctx.People.AddRange(new Person { Id = 3, Name = "Carol", Age = 40 }, new Person { Id = 4, Name = "Dave", Age = 29 });
            }
            ctx.SaveChanges();
        }
        return ctx;
    }


    private static IAsyncEnumerable<object> UnorderedConcurrentMerge(IEnumerable<IAsyncEnumerable<object>> streams, CancellationToken ct)
        => Shardis.Query.Internals.UnorderedMerge.Merge(streams, ct);

    private static async IAsyncEnumerable<object> SlowSecondShardMerge(IEnumerable<IAsyncEnumerable<object>> sources, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct, int slowDelayMs)
    {
        var list = sources.ToList();
        var channel = System.Threading.Channels.Channel.CreateUnbounded<object>();
        var tasks = list.Select((src, idx) => Task.Run(async () =>
        {
            try
            {
                await foreach (var item in src.WithCancellation(ct))
                {
                    if (idx == 1) { await Task.Delay(slowDelayMs, ct); }
                    await channel.Writer.WriteAsync(item, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        }, ct)).ToList();
        _ = Task.WhenAll(tasks).ContinueWith(t => channel.Writer.TryComplete(t.Exception), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        await foreach (var item in channel.Reader.ReadAllAsync(ct)) { yield return item; }
    }
}

internal static class StringHelper
{
    public static string ReverseStringStatic(string s) => new string(s.Reverse().ToArray());
}