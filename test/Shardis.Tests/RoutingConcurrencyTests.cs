using System.Collections.Concurrent;

using Shardis.Hashing;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;

namespace Shardis.Tests;

public class RoutingConcurrencyTests
{
    private static readonly IReadOnlyList<IShard<string>> Shards = new List<IShard<string>>
    {
        new SimpleShard(new("s1"), "c1"),
        new SimpleShard(new("s2"), "c2"),
        new SimpleShard(new("s3"), "c3"),
        new SimpleShard(new("s4"), "c4"),
    };

    [Fact]
    public async Task DefaultRouter_ShouldYieldSingleAssignment_UnderParallelRequests()
    {
        // arrange
        var store = new InMemoryShardMapStore<string>();
        var router = new DefaultShardRouter<string, string>(store, Shards, StringShardKeyHasher.Instance);
        var key = new ShardKey<string>("tenant-42");
        var shardIds = new ConcurrentBag<string>();

        // act
        await Parallel.ForEachAsync(Enumerable.Range(0, 200), async (_, _) =>
        {
            var shard = router.RouteToShard(key);
            shardIds.Add(shard.ShardId.Value);
            await Task.Yield();
        });

        // assert
        shardIds.Distinct().Count().Should().Be(1);
    }

    [Fact]
    public async Task ConsistentRouter_ShouldYieldSingleAssignment_UnderParallelRequests()
    {
        // arrange
        var store = new InMemoryShardMapStore<string>();
        var router = new ConsistentHashShardRouter<IShard<string>, string, string>(store, Shards, StringShardKeyHasher.Instance);
        var key = new ShardKey<string>("tenant-9001");
        var shardIds = new ConcurrentBag<string>();

        // act
        await Parallel.ForEachAsync(Enumerable.Range(0, 200), async (_, _) =>
        {
            var shard = router.RouteToShard(key);
            shardIds.Add(shard.ShardId.Value);
            await Task.Yield();
        });

        // assert
        shardIds.Distinct().Count().Should().Be(1);
    }
}