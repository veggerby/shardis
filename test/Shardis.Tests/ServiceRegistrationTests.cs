using Microsoft.Extensions.DependencyInjection;

using Shardis.Hashing;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;

namespace Shardis.Tests;

public class ServiceRegistrationTests
{
    [Fact]
    public void Can_Register_DefaultRouter()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddShardis<IShard<string>, string, string>(opt =>
        {
            opt.UseConsistentHashing = false;
            opt.Shards.Add(new SimpleShard(new("a"), "c1"));
            opt.Shards.Add(new SimpleShard(new("b"), "c2"));
        });
        var provider = services.BuildServiceProvider();

        // act
        var router = provider.GetRequiredService<IShardRouter<string, string>>();
        var shard = router.RouteToShard(new("user1"));

        // assert
        shard.Should().NotBeNull();
    }

    [Fact]
    public void Can_Register_ConsistentHashRouter_With_Fnv()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddShardis<IShard<string>, string, string>(opt =>
        {
            opt.UseConsistentHashing = true;
            opt.RingHasher = Fnv1aShardRingHasher.Instance;
            opt.ReplicationFactor = 25;
            opt.Shards.Add(new SimpleShard(new("a"), "c1"));
            opt.Shards.Add(new SimpleShard(new("b"), "c2"));
            opt.Shards.Add(new SimpleShard(new("c"), "c3"));
        });
        var provider = services.BuildServiceProvider();

        // act
        var router = provider.GetRequiredService<IShardRouter<string, string>>();
        var shard = router.RouteToShard(new("user1"));

        // assert
        shard.Should().NotBeNull();
    }

    [Fact]
    public void Can_Override_MapStore()
    {
        // arrange
        var services = new ServiceCollection();
        var customStore = new InMemoryShardMapStore<string>();
        services.AddSingleton<IShardMapStore<string>>(customStore); // pre-register
        services.AddShardis<IShard<string>, string, string>(opt =>
        {
            opt.UseConsistentHashing = false;
            opt.Shards.Add(new SimpleShard(new("a"), "c1"));
        });
        var provider = services.BuildServiceProvider();

        // act
        var resolved = provider.GetRequiredService<IShardMapStore<string>>();

        // assert
        resolved.Should().BeOfType(customStore.GetType());
    }

    [Fact]
    public void AddShardis_ShouldThrow_WhenReplicationFactorInvalid()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        Action act = () => services.AddShardis<IShard<string>, string, string>(opt =>
        {
            opt.ReplicationFactor = 0; // invalid
            opt.Shards.Add(new SimpleShard(new("a"), "c1"));
        });

        // assert
        act.Should().Throw<ShardisException>();
    }
}