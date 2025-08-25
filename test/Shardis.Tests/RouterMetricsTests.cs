using Shardis.Hashing;
using Shardis.Instrumentation;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;

namespace Shardis.Tests;

public class RouterMetricsTests
{
    private sealed class TestMetrics : IShardisMetrics
    {
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public List<(string router, string shard, bool existing)> Events { get; } = new();
        public void RouteHit(string router, string shardId, bool existingAssignment)
        {
            Hits++; Events.Add((router, shardId, existingAssignment));
        }
        public void RouteMiss(string router) => Misses++;
    }

    [Fact]
    public void DefaultRouter_Should_Report_Miss_Then_Hit()
    {
        // arrange
        var metrics = new TestMetrics();
        var shards = new List<IShard<string>>
        {
            new SimpleShard(new("s1"), "c1"),
            new SimpleShard(new("s2"), "c2"),
        };
        var store = new InMemoryShardMapStore<string>();
        var router = new DefaultShardRouter<string, string>(store, shards, StringShardKeyHasher.Instance, metrics);
        var key = new ShardKey<string>("user-1");

        // act
        var shard1 = router.RouteToShard(key);
        var shard2 = router.RouteToShard(key);

        // assert
        shard1.Should().Be(shard2);
        metrics.Misses.Should().Be(1);
        metrics.Hits.Should().Be(2); // existing + new assignment
        metrics.Events.Should().Contain(e => !e.existing);
        metrics.Events.Should().Contain(e => e.existing);
    }

    [Fact]
    public void ConsistentRouter_Should_Report_Miss_Then_Hit()
    {
        // arrange
        var metrics = new TestMetrics();
        var shards = new List<IShard<string>>
        {
            new SimpleShard(new("s1"), "c1"),
            new SimpleShard(new("s2"), "c2"),
            new SimpleShard(new("s3"), "c3"),
        };
        var store = new InMemoryShardMapStore<string>();
        var router = new ConsistentHashShardRouter<IShard<string>, string, string>(store, shards, StringShardKeyHasher.Instance, 50, Fnv1aShardRingHasher.Instance, metrics);
        var key = new ShardKey<string>("user-xyz");

        // act
        var shard1 = router.RouteToShard(key);
        var shard2 = router.RouteToShard(key);

        // assert
        shard1.Should().Be(shard2);
        metrics.Misses.Should().Be(1);
        metrics.Hits.Should().Be(2);
    }
}