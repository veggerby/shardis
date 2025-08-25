using System.Collections.Concurrent;

using Shardis.Hashing;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;

namespace Shardis.Tests;

public class MapStoreTryGetOrAddTests
{
    [Fact]
    public void DefaultRouter_Should_Use_Single_Assignment_Under_Concurrency()
    {
        // arrange
        var store = new InMemoryShardMapStore<string>();
        var shards = new List<IShard<string>> { new SimpleShard(new("s1"), "c1"), new SimpleShard(new("s2"), "c2") };
        var router = new DefaultShardRouter<string, string>(store, shards, StringShardKeyHasher.Instance);
        var key = new ShardKey<string>("hot");
        var shardIds = new ConcurrentBag<ShardId>();

        // act
        Parallel.ForEach(Enumerable.Range(0, 5000), _ =>
        {
            var assignment = router.Route(key);
            shardIds.Add(assignment.Shard.ShardId);
        });

        // assert
        shardIds.Should().OnlyContain(id => id == shardIds.First());
    }

    [Fact]
    public void ConsistentRouter_Should_Use_Single_Assignment_Under_Concurrency()
    {
        // arrange
        var store = new InMemoryShardMapStore<string>();
        var shards = new List<IShard<string>> { new SimpleShard(new("s1"), "c1"), new SimpleShard(new("s2"), "c2") };
        var router = new ConsistentHashShardRouter<IShard<string>, string, string>(store, shards, StringShardKeyHasher.Instance, 50);
        var key = new ShardKey<string>("hot");
        var shardIds = new ConcurrentBag<ShardId>();

        // act
        Parallel.ForEach(Enumerable.Range(0, 5000), _ =>
        {
            var assignment = router.Route(key);
            shardIds.Add(assignment.Shard.ShardId);
        });

        // assert
        shardIds.Should().OnlyContain(id => id == shardIds.First());
    }
}