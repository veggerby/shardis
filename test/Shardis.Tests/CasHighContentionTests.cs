using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Tests;

public class CasHighContentionTests
{
    [Fact]
    public void TryAssignShardToKey_Should_Succeed_Only_Once_Under_Contention()
    {
        // arrange
        var store = new InMemoryShardMapStore<string>();
        var key = new ShardKey<string>("hot-key");
        var shard = new ShardId("primary");
        int winners = 0;

        // act
        Parallel.ForEach(Enumerable.Range(0, 10_000), _ =>
        {
            var created = store.TryAssignShardToKey(key, shard, out var map);
            if (created)
            {
                Interlocked.Increment(ref winners);
            }
        });

        // assert
        winners.Should().Be(1);
        store.TryGetShardIdForKey(key, out var finalId).Should().BeTrue();
        finalId.Should().Be(shard);
    }
}