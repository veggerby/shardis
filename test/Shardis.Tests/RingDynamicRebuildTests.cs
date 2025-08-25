using System.Collections.Concurrent;

using Shardis.Hashing;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;

namespace Shardis.Tests;

public class RingDynamicRebuildTests
{
    [Fact]
    public async Task Concurrent_Routes_During_Add_And_Remove_Should_Not_Throw()
    {
        // arrange
        var store = new InMemoryShardMapStore<string>();
        var shards = new List<IShard<string>>
        {
            new SimpleShard(new("s1"), "c1"),
            new SimpleShard(new("s2"), "c2"),
        };
        var router = new ConsistentHashShardRouter<IShard<string>, string, string>(store, shards, StringShardKeyHasher.Instance, 30);
        var cts = new CancellationTokenSource();
        var errors = new ConcurrentBag<Exception>();

        // act
        var routingTask = Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < 5_000; i++)
                    {
                        var key = new ShardKey<string>("k" + (i % 100));
                        _ = router.Route(key);
                    }
                }
                catch (Exception ex) { errors.Add(ex); }
            });

        var mutateTask = Task.Run(() =>
        {
            try
            {
                router.AddShard(new SimpleShard(new("s3"), "c3"));
                var removed = router.RemoveShard(new("s1"));
                removed.Should().BeTrue();
            }
            catch (Exception ex) { errors.Add(ex); }
        });

        await Task.WhenAll(routingTask, mutateTask);

        // assert
        errors.Should().BeEmpty();
    }
}