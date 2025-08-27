using Microsoft.Extensions.DependencyInjection;

using Shardis.Model;
using Shardis.Routing;

namespace Shardis.Tests;

public class ReplicationFactorValidationTests
{
    [Fact]
    public void AddShardis_ShouldThrow_WhenReplicationFactorTooHigh()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        Action act = () => services.AddShardis<IShard<string>, string, string>(opt =>
        {
            opt.ReplicationFactor = 10_001;
            opt.Shards.Add(new SimpleShard(new("s1"), "c1"));
        });

        // assert
        act.Should().Throw<ShardisException>();
    }

    [Fact]
    public void ConsistentHashRouter_ShouldThrow_WhenReplicationFactorTooHigh()
    {
        // arrange
        var store = new Shardis.Persistence.InMemoryShardMapStore<string>();
        var shards = new List<IShard<string>> { new SimpleShard(new("s1"), "c1") };

        // act
        Action act = () => _ = new ConsistentHashShardRouter<IShard<string>, string, string>(store, shards, Shardis.Hashing.StringShardKeyHasher.Instance, 10_001);

        // assert
        act.Should().Throw<ShardisException>();
    }
}