using Shardis.Migration.Topology;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Tests;

public class ShardMapEnumerationTests
{
    // arrange helpers
    private static InMemoryShardMapStore<string> Seed(int count, int shards)
    {
        var store = new InMemoryShardMapStore<string>();
        for (int i = 0; i < count; i++)
        {
            var shardId = new ShardId((i % shards).ToString());
            store.AssignShardToKey(new ShardKey<string>($"k-{i:000}"), shardId);
        }
        return store;
    }

    [Fact]
    public async Task Enumerate_ToSnapshot_RoundTrips()
    {
        // arrange
        var store = Seed(100, 4);

        // act
        var snapshot = await store.ToSnapshotAsync();

        // assert
        Assert.Equal(100, snapshot.Assignments.Count);
        for (int i = 0; i < 100; i++)
        {
            var key = new ShardKey<string>($"k-{i:000}");
            var expected = new ShardId((i % 4).ToString());
            Assert.Equal(expected, snapshot.Assignments[key]);
        }
    }

    [Fact]
    public async Task Enumerate_Cancellation_Throws()
    {
        // arrange
        var store = Seed(200, 2);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act/assert
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await store.ToSnapshotAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Enumerate_MaxKeysExceeded_Throws()
    {
        // arrange
        var store = Seed(50, 3);

        // act/assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.ToSnapshotAsync(maxKeys: 10));
        Assert.Contains("Snapshot key cap", ex.Message);
    }
}