using System.Collections.Concurrent;

using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Tests;

public class CasContentionTests
{
    [Fact]
    public async Task TryAssignShardToKey_ShouldOnlyCreateOnce_UnderContention()
    {
        // arrange
        var store = new InMemoryShardMapStore<string>();
        var key = new ShardKey<string>("tenant-x");
        var shardIds = new[] { new ShardId("s1"), new ShardId("s2"), new ShardId("s3") };
        var winners = new ConcurrentBag<ShardId>();

        // act
        await Parallel.ForEachAsync(Enumerable.Range(0, 200), async (i, _) =>
        {
            var chosen = shardIds[i % shardIds.Length];
            if (store.TryAssignShardToKey(key, chosen, out var map))
            {
                winners.Add(map.ShardId);
            }
            await Task.Yield();
        });

        // assert
        (winners.Count < 2).Should().BeTrue($"Expected at most one winner, got {winners.Count}");
        store.TryGetShardIdForKey(key, out var finalId).Should().BeTrue();
        shardIds.Should().Contain(finalId);
    }
}