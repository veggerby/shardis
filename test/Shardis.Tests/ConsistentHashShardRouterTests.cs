using Shardis.Hashing;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;

namespace Shardis.Tests;

public class ConsistentHashShardRouterTests
{
    [Fact]
    public void RouteToShard_ShouldAssignShardDeterministically()
    {
        // arrange
        var shards = new List<IShard<string>>
        {
            new SimpleShard(new("shard-001"), "connection-1"),
            new SimpleShard(new("shard-002"), "connection-2"),
            new SimpleShard(new("shard-003"), "connection-3")
        };

        var shardMapStore = new InMemoryShardMapStore<string>();
        var router = new ConsistentHashShardRouter<IShard<string>, string, string>(shardMapStore, shards, StringShardKeyHasher.Instance);

        var shardKey = new ShardKey<string>("user-123");

        // act
        var assignedShard = router.RouteToShard(shardKey);

        // assert
        assignedShard.Should().NotBeNull();
        shards.Should().Contain(assignedShard);
    }

    [Fact]
    public void RouteToShard_ShouldReturnSameShardForSameKey()
    {
        // arrange
        var shards = new List<IShard<string>>
        {
            new SimpleShard(new("shard-001"), "connection-1"),
            new SimpleShard(new("shard-002"), "connection-2"),
            new SimpleShard(new("shard-003"), "connection-3")
        };

        var shardMapStore = new InMemoryShardMapStore<string>();
        var router = new ConsistentHashShardRouter<IShard<string>, string, string>(shardMapStore, shards, StringShardKeyHasher.Instance);

        var shardKey = new ShardKey<string>("user-123");

        // act
        var firstAssignment = router.RouteToShard(shardKey);
        var secondAssignment = router.RouteToShard(shardKey);

        // assert
        firstAssignment.Should().BeSameAs(secondAssignment);
    }
}