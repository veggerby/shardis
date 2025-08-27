using BenchmarkDotNet.Attributes;

using Shardis.Hashing;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;

namespace Shardis.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
[BenchmarkCategory("router")]
public class RouterBenchmarks
{
    private readonly IShardRouter<string, string> _defaultRouter;
    private readonly IShardRouter<string, string> _consistentRouter;
    private readonly ShardKey<string>[] _keys;

    public RouterBenchmarks()
    {
        var shards = new[]
        {
            new SimpleShard(new("a"), "c1"),
            new SimpleShard(new("b"), "c2"),
            new SimpleShard(new("c"), "c3"),
            new SimpleShard(new("d"), "c4"),
        };
        var store1 = new InMemoryShardMapStore<string>();
        var store2 = new InMemoryShardMapStore<string>();
        var hasher = DefaultShardKeyHasher<string>.Instance;
        _defaultRouter = new DefaultShardRouter<string, string>(store1, shards, hasher);
        _consistentRouter = new ConsistentHashShardRouter<SimpleShard, string, string>(store2, shards, hasher, replicationFactor: 100, ringHasher: DefaultShardRingHasher.Instance);

        _keys = Enumerable.Range(0, 10_000)
            .Select(i => new ShardKey<string>($"user-{i:000000}"))
            .ToArray();
    }

    [Benchmark]
    public int DefaultRouter_RouteMany()
    {
        int sum = 0;
        foreach (var key in _keys)
        {
            var shard = _defaultRouter.RouteToShard(key);
            // add index of shard for baseline work
            sum += shard.ShardId.Value[0];
        }
        return sum;
    }

    [Benchmark]
    public int ConsistentRouter_RouteMany()
    {
        int sum = 0;
        foreach (var key in _keys)
        {
            var shard = _consistentRouter.RouteToShard(key);
            sum += shard.ShardId.Value[0];
        }
        return sum;
    }
}

// Entry point removed; central benchmark switcher in Program.cs handles execution.