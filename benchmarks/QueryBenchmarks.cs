using BenchmarkDotNet.Attributes;

using Shardis.Query;
using Shardis.Query.InMemory.Execution;
using Shardis.Query.Internals;

[MemoryDiagnoser]
public class QueryBenchmarks
{
    private InMemoryShardQueryExecutor _exec = null!;

    [GlobalSetup]
    public void Setup()
    {
        var shard1 = Enumerable.Range(0, 1000).Select(i => (object)new Person { Id = i, Age = i % 80, Name = "P" + i });
        var shard2 = Enumerable.Range(1000, 1000).Select(i => (object)new Person { Id = i, Age = i % 80, Name = "P" + i });
        _exec = new InMemoryShardQueryExecutor(new[] { shard1, shard2 }, (streams, ct) => UnorderedMerge.Merge(streams, ct));
    }

    [Benchmark]
    public async Task<int> ShardQuery_WhereSelect_Count()
    {
        var q = ShardQuery.For<Person>(_exec).Where(p => p.Age > 40).Select(p => p.Age);
        var list = await q.ToListAsync();
        return list.Count;
    }

    [Benchmark]
    public int Linq_Baseline_WhereSelect_Count()
    {
        var shard1 = _exec.GetType().GetField("_shards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(_exec) as IReadOnlyList<IEnumerable<object>>;
        var all = shard1![0].Concat(shard1[1]).Cast<Person>();
        return all.Where(p => p.Age > 40).Select(p => p.Age).Count();
    }

    private sealed class Person { public int Id { get; set; } public int Age { get; set; } public string Name { get; set; } = string.Empty; }
}