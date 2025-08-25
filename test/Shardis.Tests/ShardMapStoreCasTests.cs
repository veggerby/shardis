using NSubstitute;
using Shardis.Model;
using Shardis.Persistence;
using Xunit;

namespace Shardis.Tests;

public class ShardMapStoreCasTests
{
    private static readonly ShardId Shard1 = new("s1");

    [Fact]
    public void TryAssign_FirstWins_ReturnsTrue()
    {
        // arrange
        var store = new InMemoryShardMapStore<string>();
        var key = new ShardKey<string>("k1");

        // act
        var created = store.TryAssignShardToKey(key, Shard1, out var map);

        // assert
        Assert.True(created);
        Assert.Equal(Shard1, map.ShardId);
    }

    [Fact]
    public void TryAssign_Duplicate_ReturnsFalseAndKeepsOriginal()
    {
        // arrange
        var store = new InMemoryShardMapStore<string>();
        var key = new ShardKey<string>("k1");
        store.AssignShardToKey(key, Shard1);
        var other = new ShardId("s2");

        // act
        var created = store.TryAssignShardToKey(key, other, out var map);

        // assert
        Assert.False(created);
        Assert.Equal(Shard1, map.ShardId);
    }
}
