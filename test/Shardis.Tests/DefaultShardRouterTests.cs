using AwesomeAssertions;

using NSubstitute;

using Shardis.Hashing;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;

namespace Shardis.Tests;

public class DefaultShardRouterTests
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

        var shardMapStore = Substitute.For<IShardMapStore<string>>();
        var router = new DefaultShardRouter<string, string>(shardMapStore, shards, StringShardKeyHasher.Instance);

        var shardKey = new ShardKey<string>("user-123");

        // act
        var assignedShard = router.RouteToShard(shardKey);

        // assert
        assignedShard.ShouldNotBeNull();
        shards.Contains(assignedShard).ShouldBeTrue();
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

        var shardMapStore = Substitute.For<IShardMapStore<string>>();
        var router = new DefaultShardRouter<string, string>(shardMapStore, shards, StringShardKeyHasher.Instance);

        var shardKey = new ShardKey<string>("user-123");

        // act
        var firstAssignment = router.RouteToShard(shardKey);
        var secondAssignment = router.RouteToShard(shardKey);

        // assert
        firstAssignment.ShouldBeSameAs(secondAssignment);
    }
}