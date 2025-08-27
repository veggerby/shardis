using BenchmarkDotNet.Attributes;

using Marten;

using Shardis.Marten;
using Shardis.Query.Marten;

namespace Shardis.Benchmarks;

// Category: query
[MemoryDiagnoser]
[SimpleJob]
public class AdaptivePagingBenchmarks
{
    private DocumentStore? _store;
    private MartenShard? _shard;
    private IDocumentSession? _session;
    private const int SeedCount = 2000;

    [GlobalSetup]
    public async Task Setup()
    {
        var conn = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn)) throw new InvalidOperationException("POSTGRES_CONNECTION not set for benchmark");
        _store = DocumentStore.For(o => o.Connection(conn));
        _shard = new MartenShard(new("bench-shard"), _store);
        await using var s = _shard.CreateSession();
        // Seed once (idempotent simplistic)
        if (!s.Query<Person>().Any())
        {
            for (int i = 0; i < SeedCount; i++)
            {
                s.Store(new Person { Id = Guid.NewGuid(), Name = "P" + i, Age = 20 + (i % 50) });
            }
            await s.SaveChangesAsync();
        }
        _session = _shard.CreateSession();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _session?.Dispose();
        _store?.Dispose();
    }

    [Benchmark(Description = "Fixed paging size=256")]
    public async Task FixedPaging()
    {
        var exec = MartenQueryExecutor.Instance.WithPageSize(256);
        int count = 0;
        await foreach (var _ in exec.Execute<Person>(_session!, q => q.Where(p => p.Age > 0).Select(p => p)))
        {
            if (++count == 500) break; // partial enumeration scenario
        }
    }

    [Benchmark(Description = "Adaptive paging 64-1024 target=50ms")]
    public async Task AdaptivePaging()
    {
        var exec = MartenQueryExecutor.Instance.WithAdaptivePaging(minPageSize: 64, maxPageSize: 1024, targetBatchMilliseconds: 50);
        int count = 0;
        await foreach (var _ in exec.Execute<Person>(_session!, q => q.Where(p => p.Age > 0).Select(p => p)))
        {
            if (++count == 500) break;
        }
    }

    private sealed class Person { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; public int Age { get; set; } }
}