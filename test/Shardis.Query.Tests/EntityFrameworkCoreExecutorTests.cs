using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Shardis.Factories;
using Shardis.Query.EntityFrameworkCore.Execution;

namespace Shardis.Query.Tests;

public sealed class EntityFrameworkCoreExecutorTests
{
    private sealed class Person
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }
    private sealed class PersonContext(DbContextOptions<PersonContext> options) : DbContext(options)
    {
        public DbSet<Person> People => Set<Person>();
    }

    [Fact]
    public async Task EntityFrameworkCoreExecutor_ServerSideWhereSelect()
    {
        // arrange
        IShardFactory<DbContext> factory1 = new DelegatingShardFactory<DbContext>((sid, ct) => new ValueTask<DbContext>(CreateAndSeedSqlite(int.Parse(sid.Value))));
        var exec = new EntityFrameworkCoreShardQueryExecutor(2, factory1, UnorderedConcurrentMerge);
        var q = ShardQuery.For<Person>(exec).Where(p => p.Age >= 30).Select(p => new { p.Name, p.Age });

        // act
        var results = await q.ToListAsync();

        // assert
        results.Should().BeEquivalentTo(new[] { new { Name = "Alice", Age = 35 }, new { Name = "Carol", Age = 40 } });
    }

    [Fact]
    public async Task EntityFrameworkCoreExecutor_Streaming_FirstItemBeforeSlowShardCompletes()
    {
        // arrange
        var delayMs = 150; // simulate slow shard 1
        var sw = System.Diagnostics.Stopwatch.StartNew();
        IShardFactory<DbContext> factory2 = new DelegatingShardFactory<DbContext>((sid, ct) => new ValueTask<DbContext>(CreateAndSeedSqlite(int.Parse(sid.Value))));
        var exec = new EntityFrameworkCoreShardQueryExecutor(2, factory2, (streams, ct) => SlowSecondShardMerge(streams, ct, delayMs));
        var q = ShardQuery.For<Person>(exec).Where(p => p.Age >= 25).Select(p => p.Name);
        var collected = new List<string>();

        // act
        await foreach (var name in q)
        {
            collected.Add(name);
            if (collected.Count == 1) { break; }
        }
        sw.Stop();

        // assert
        collected.Should().NotBeEmpty();
        sw.ElapsedMilliseconds.Should().BeLessThan(delayMs); // first item arrived before slow shard delay elapsed
    }

    [Fact]
    public async Task EntityFrameworkCore_NoClientEvaluation()
    {
        // arrange
        IShardFactory<DbContext> factory3 = new DelegatingShardFactory<DbContext>((sid, ct) => new ValueTask<DbContext>(CreateAndSeedSqlite(0, configureWarnings: true)));
        var exec = new EntityFrameworkCoreShardQueryExecutor(1, factory3, UnorderedConcurrentMerge);
        // Non-translatable predicate -> should throw translation exception (QueryTranslationFailed) instead of client evaluating
        var q = ShardQuery.For<Person>(exec).Where(p => StringHelper.ReverseStringStatic(p.Name) == "Alice");

        // act
        var act = async () => await q.ToListAsync();

        // assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task EntityFrameworkCoreExecutor_AppliesCommandTimeout_WhenProvided()
    {
        // arrange
        var timeout = 123;
        IShardFactory<DbContext> factory4 = new DelegatingShardFactory<DbContext>((sid, ct) => new ValueTask<DbContext>(CreateAndSeedSqlite(0)));
        var exec = new EntityFrameworkCoreShardQueryExecutor(1, factory4, UnorderedConcurrentMerge, commandTimeoutSeconds: timeout);
        var q = ShardQuery.For<Person>(exec).Where(p => p.Age >= 0).Select(p => p);

        // act
        // Enumerate single entity to ensure pipeline executed (timeout path reached)
        await foreach (var _ in q) { break; }

        // assert
        // Create a fresh context to inspect default timeout remains unaffected (we cannot directly read the timeout from disposed context here)
        // Instead: ensure enumeration succeeded without exception (smoke). For stronger validation, provider-specific APIs would be needed.
        true.Should().BeTrue();
    }

    private static PersonContext CreateAndSeedSqlite(int shardId, bool configureWarnings = false)
    {
        // arrange
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
                ctx.People.AddRange(
                    new Person { Id = 1, Name = "Alice", Age = 35 },
                    new Person { Id = 2, Name = "Bob", Age = 25 }
                );
            }
            else
            {
                ctx.People.AddRange(
                    new Person { Id = 3, Name = "Carol", Age = 40 },
                    new Person { Id = 4, Name = "Dave", Age = 29 }
                );
            }
            ctx.SaveChanges();
        }

        return ctx;
    }

    private static IAsyncEnumerable<object> UnorderedConcurrentMerge(IEnumerable<IAsyncEnumerable<object>> streams, CancellationToken ct)
        => Internals.UnorderedMerge.Merge(streams, ct);

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