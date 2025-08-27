using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using Shardis.Model;
using Shardis.Querying;
using Shardis.Querying.Linq;

namespace Shardis.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("merge")]
[Config(typeof(Config))]
public class MergeEnumeratorBenchmarks
{
    [Params(2, 4, 8)] public int Shards { get; set; }
    [Params(1000, 10000)] public int ItemsPerShard { get; set; }
    [Params(1, 3, 10)] public int SkewFactor { get; set; } // multiplier for slowest shard delay
    [Params(1, 2, 4)] public int PrefetchPerShard { get; set; }

    private ShardStreamBroadcaster<IShard<int>, int> _broadcaster = null!;
    private TestShard[] _shards = null!;

    [GlobalSetup]
    public void Setup()
    {
        _shards = new TestShard[Shards];
        for (int i = 0; i < Shards; i++)
        {
            // Slow last shard by skew factor relative to base 0 delay; use small delay to amplify differences
            int delay = i == Shards - 1 && SkewFactor > 1 ? SkewFactor : 1;
            _shards[i] = new TestShard(i, $"s{i}", ItemsPerShard, delay);
        }
        _broadcaster = new ShardStreamBroadcaster<IShard<int>, int>(_shards);
    }

    [Benchmark(Description = "OrderedStreaming_TotalItems")]
    public async Task<int> OrderedStreaming_TotalItems()
    {
        int count = 0;
        var sw = Stopwatch.StartNew();
        long firstItemMicros = -1;
        await foreach (var item in _broadcaster.QueryAllShardsOrderedStreamingAsync<int, int>(session => _shards[session].Stream(), x => x, prefetchPerShard: PrefetchPerShard))
        {
            count++;
            if (firstItemMicros < 0) { firstItemMicros = sw.ElapsedTicks * 1_000_000 / Stopwatch.Frequency; }
        }
    FirstItemMicros = firstItemMicros;
        return count;
    }

    [Benchmark(Description = "UnorderedStreaming_TotalItems")]
    public async Task<int> UnorderedStreaming_TotalItems()
    {
        int count = 0;
        var sw = Stopwatch.StartNew();
        long firstItemMicros = -1;
        await foreach (var item in _broadcaster.QueryAllShardsAsync<int>(session => _shards[session].Stream()))
        {
            count++;
            if (firstItemMicros < 0) { firstItemMicros = sw.ElapsedTicks * 1_000_000 / Stopwatch.Frequency; }
        }
        FirstItemMicros = firstItemMicros;
        return count;
    }
    public long FirstItemMicros { get; private set; }

    private sealed class Config : ManualConfig { }

    [Benchmark(Description = "OrderedStreaming_FirstItemLatency_us")]
    public long OrderedStreaming_FirstItemLatency()
    {
        // Re-run a tiny ordered session to isolate first item cost
        var shard = new TestShard(0, "s0", 10, 1);
        var small = new ShardStreamBroadcaster<IShard<int>, int>(new[] { shard });
        var sw = Stopwatch.StartNew();
        long first = -1;
        var task = small.QueryAllShardsOrderedStreamingAsync<int, int>(_ => shard.Stream(), x => x).GetAsyncEnumerator();
        try
        {
            if (task.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                first = sw.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;
            }
        }
        finally { task.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        return first;
    }

    private sealed class TestShard(int index, string id, int count, int delayFactor) : IShard<int>
    {
        public ShardId ShardId { get; } = new(id);
        public int CreateSession() => index;
        public IShardQueryExecutor<int> QueryExecutor => new DummyExecutor();
        public async IAsyncEnumerable<int> Stream()
        {
            for (int i = 0; i < count; i++)
            {
                if (delayFactor > 1 && i % 50 == 0)
                {
                    await Task.Delay(delayFactor); // coarse slowing
                }
                yield return i;
            }
        }

        private sealed class DummyExecutor : Shardis.Querying.Linq.IShardQueryExecutor<int>
        {
            public IAsyncEnumerable<T> Execute<T>(int session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IQueryable<T>>> linqExpr) where T : notnull => throw new NotSupportedException();
            public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(int session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr, Func<T, TKey> keySelector) where T : notnull => throw new NotSupportedException();
        }
    }
}