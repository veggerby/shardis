using BenchmarkDotNet.Attributes;

using Marten;

using Shardis.Marten;

namespace Shardis.Benchmarks;

// Category: query
[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 5)] // keep short to reduce CI variance
public class AdaptivePagingAllocationsBenchmarks
{
    private DocumentStore? _store;
    private MartenShard? _shard;
    private MartenQueryExecutor? _fixedExec;
    private MartenQueryExecutor? _adaptiveExec;
    private const int SeedCount = 5000;
    private static readonly Random Rng = new(SeedSeed);
    private const int SeedSeed = 1337; // deterministic seed for future randomized fields

    [Params(250)] public int TakeCount; // partial enumeration size

    [GlobalSetup]
    public async Task Setup()
    {
        var conn = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn)) throw new InvalidOperationException("POSTGRES_CONNECTION not set for benchmark");
        _store = DocumentStore.For(o => o.Connection(conn));
        _shard = new MartenShard(new("alloc-shard"), _store);
        await using var s = _shard.CreateSession();
        if (!s.Query<Person>().Any())
        {
            for (int i = 0; i < SeedCount; i++)
            {
                // deterministic content; avoid randomness until needed (SeedSeed reserved)
                s.Store(new Person { Id = Guid.NewGuid(), Name = "P" + i, Age = 18 + (i % 40) });
            }
            await s.SaveChangesAsync();
        }
        _fixedExec = MartenQueryExecutor.Instance.WithPageSize(256);
        _adaptiveExec = MartenQueryExecutor.Instance.WithAdaptivePaging(minPageSize: 64, maxPageSize: 1024, targetBatchMilliseconds: 50);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _store?.Dispose();
    }

    [Benchmark(Description = "Fixed paging allocations")]
    public async Task Fixed()
    {
        await using var session = _shard!.CreateSession();
        int count = 0;
        await foreach (var _ in _fixedExec!.Execute<Person>(session, q => q.Where(p => p.Age > 0).Select(p => p)))
        {
            if (++count >= TakeCount) break;
        }
    }

    [Benchmark(Description = "Adaptive paging allocations")]
    public async Task Adaptive()
    {
        await using var session = _shard!.CreateSession();
        int count = 0;
        await foreach (var _ in _adaptiveExec!.Execute<Person>(session, q => q.Where(p => p.Age > 0).Select(p => p)))
        {
            if (++count >= TakeCount) break;
        }
    }

    private sealed class Person { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; public int Age { get; set; } }
}