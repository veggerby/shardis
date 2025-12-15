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
        snapshot.Assignments.Should().HaveCount(100);
        for (int i = 0; i < 100; i++)
        {
            var key = new ShardKey<string>($"k-{i:000}");
            var expected = new ShardId((i % 4).ToString());
            snapshot.Assignments[key].Should().Be(expected);
        }
    }

    [Fact]
    public async Task Enumerate_Cancellation_Throws()
    {
        // arrange
        var store = Seed(200, 2);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act
        Func<Task> act = () => store.ToSnapshotAsync(cancellationToken: cts.Token);

        // assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Enumerate_MaxKeysExceeded_Throws()
    {
        // arrange
        var store = Seed(50, 3);

        // act
        Func<Task> act = () => store.ToSnapshotAsync(maxKeys: 10);

        // assert
        var ex = await act.Should().ThrowAsync<ShardTopologyException>();
        ex.Which.Message.Should().Contain("Snapshot key cap");
    }
}