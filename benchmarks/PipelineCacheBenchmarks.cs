using BenchmarkDotNet.Attributes;

using Shardis.Query;
using Shardis.Query.InMemory.Execution;
using Shardis.Query.Internals;

namespace Shardis.Benchmarks;

[MemoryDiagnoser]
public class PipelineCacheBenchmarks
{
    private InMemoryShardQueryExecutor _executor = null!;
    private object[] _shard1 = null!;
    private object[] _shard2 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _shard1 = Enumerable.Range(0, 4000).Select(i => (object)new Person { Age = i % 80, Id = i }).ToArray();
        _shard2 = Enumerable.Range(4000, 4000).Select(i => (object)new Person { Age = i % 80, Id = i }).ToArray();
        _executor = new InMemoryShardQueryExecutor(new[] { _shard1, _shard2 }, (s, ct) => UnorderedMerge.Merge(s, ct));
    }

    [Benchmark(Description = "Cached executor (pipeline reused)")]
    public int Cached()
    {
        var q = ShardQuery.For<Person>(_executor).Where(p => p.Age > 30).Select(p => p.Age);
        var result = q.ToListAsync().GetAwaiter().GetResult();
        File.AppendAllText("PipelineCacheBenchmarks.compilecount.txt", InMemoryShardQueryExecutor.TotalCompiledPipelines + "\n");
        return result.Count;
    }

    [Benchmark(Description = "New executor (no prior cache entry)")]
    public int Uncached()
    {
        var fresh = new InMemoryShardQueryExecutor(new[] { _shard1, _shard2 }, (s, ct) => UnorderedMerge.Merge(s, ct));
        var q = ShardQuery.For<Person>(fresh).Where(p => p.Age > 30).Select(p => p.Age);
        var result = q.ToListAsync().GetAwaiter().GetResult();
        File.AppendAllText("PipelineCacheBenchmarks.compilecount.txt", InMemoryShardQueryExecutor.TotalCompiledPipelines + "\n");
        return result.Count;
    }

    private sealed class Person { public int Id { get; set; } public int Age { get; set; } }
}