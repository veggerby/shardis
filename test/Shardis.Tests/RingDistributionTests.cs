using Shardis.Hashing;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;

namespace Shardis.Tests;

public class RingDistributionTests
{
    [Fact]
    public void ConsistentRouter_ShouldDistributeKeysReasonably()
    {
        // arrange
        var shards = Enumerable.Range(1, 8).Select(i => (IShard<string>)new SimpleShard(new($"s{i}"), $"c{i}")).ToList();
        var store = new InMemoryShardMapStore<string>();
        var router = new ConsistentHashShardRouter<IShard<string>, string, string>(store, shards, StringShardKeyHasher.Instance, replicationFactor: 120);
        var counts = new Dictionary<string, int>();
        foreach (var s in shards) counts[s.ShardId.Value] = 0;

        // act
        for (int i = 0; i < 10_000; i++)
        {
            var key = new ShardKey<string>("k" + i.ToString());
            var shard = router.RouteToShard(key);
            counts[shard.ShardId.Value]++;
        }

        // assert (rough heuristic: each shard within +/-45% of ideal due to probabilistic distribution)
        var ideal = 10000.0 / shards.Count;
        foreach (var kvp in counts)
        {
            (kvp.Value >= (int)(ideal * 0.50)).Should().BeTrue($"Shard {kvp.Key} below expected lower bound: {kvp.Value}");
            kvp.Value.Should().BeLessThan((int)(ideal * 1.55));
        }

        // variance check (coefficient of variation should be within a loose bound)
        var values = counts.Values.Select(v => (double)v).ToList();
        var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        var stdDev = Math.Sqrt(variance);
        (stdDev / mean).Should().BeLessThan(0.35); // heuristic
    }
}