using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

using Microsoft.EntityFrameworkCore;

using Shardis.Factories;
using Shardis.Model;
using Shardis.Query; // for UnorderedMergeHelper + extensions
using Shardis.Query.Diagnostics;
using Shardis.Query.EntityFrameworkCore;
using Shardis.Query.EntityFrameworkCore.Execution;

namespace Shardis.Benchmarks;

/// <summary>
/// Benchmarks the overhead of the latency emission path across unordered, ordered (buffered), and failure-handled executions.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("query-latency")]
public class QueryLatencyEmissionBenchmarks
{
    private Shardis.Query.Execution.IShardQueryExecutor _unordered = default!;
    private Shardis.Query.Execution.IShardQueryExecutor _ordered = default!;
    // best-effort currently not benchmarked (same path latency). Can be added once public decorator available.

    private sealed class Person { public int Id { get; set; } public int Age { get; set; } }
    private sealed class PersonContext : DbContext
    {
        public PersonContext(DbContextOptions<PersonContext> o) : base(o) { }
        public DbSet<Person> People => Set<Person>();
    }

    private sealed class Factory : IShardFactory<DbContext>
    {
        public ValueTask<DbContext> CreateAsync(ShardId shardId, CancellationToken ct = default)
            => new(CreateContext(int.Parse(shardId.Value)));
    }

    [GlobalSetup]
    public void Setup()
    {
        var factory = new Factory();
        // 3 shard topology
        _unordered = new EntityFrameworkCoreShardQueryExecutor(3, factory, (s, ct) => UnorderedMergeHelper.Merge(s, ct, channelCapacity: null), queryMetrics: new MetricShardisQueryMetrics());
        var ordFactory = new EfCoreShardQueryExecutor.DefaultOrderedEfCoreExecutorFactory();
        _ordered = ordFactory.CreateOrdered(_unordered, (System.Linq.Expressions.Expression<System.Func<Person, object>>)(p => p.Id), descending: false);
    }

    private static PersonContext CreateContext(int shard, int rows = 4)
    {
        // InMemory provider keeps focus on merge + latency emission overhead (avoids SQLite connection cost).
        var opt = new DbContextOptionsBuilder<PersonContext>().UseInMemoryDatabase($"bench-shard-{shard}-{Guid.NewGuid()}").Options;
        var ctx = new PersonContext(opt);

        for (int i = 0; i < rows; i++)
        {
            ctx.People.Add(new Person { Id = shard * 100 + i + 1, Age = 20 + i });
        }
        ctx.SaveChanges();

        return ctx;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<int> ConsumeAsync<T>(IAsyncEnumerable<T> src)
    {
        int count = 0;
        await foreach (var _ in src) count++;
        return count;
    }

    [Benchmark(Description = "unordered")] public async Task<int> Unordered() => await ConsumeAsync(_unordered.Query<Person>().Where(p => p.Age >= 20));
    [Benchmark(Description = "ordered")] public async Task<int> Ordered() => await ConsumeAsync(_ordered.Query<Person>().Where(p => p.Age >= 20));
}