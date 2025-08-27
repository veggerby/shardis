using Shardis.Querying;
using Shardis.Tests.TestHelpers;

namespace Shardis.Tests;

public class ShardisOrderedEnumeratorTests
{
    [Fact]
    public async Task MoveNextAsync_ShouldYieldItemsInOrder()
    {
        // arrange
        var shard1 = new TestShardisEnumerator<int>(
            items: [1, 3],
            shardId: "shard1"
        );

        var shard2 = new TestShardisEnumerator<int>(
            items: [2, 4],
            shardId: "shard2"
        );

        var enumerator = new ShardisAsyncOrderedEnumerator<int, int>(
            [shard1, shard2],
            keySelector: x => x,
            prefetchPerShard: 1,
            cancellationToken: CancellationToken.None);

        // act
        var results = new List<ShardItem<int>>();
        while (await enumerator.MoveNextAsync())
        {
            results.Add(enumerator.Current);
        }

        // assert
        results.Should().HaveCount(4);
        results.Select(r => r.Item).Should().ContainInOrder(1, 2, 3, 4);
    }

    [Fact]
    public async Task MoveNextAsync_ShouldReturnFalseWhenAllStreamsAreExhausted()
    {
        // arrange
        var shard1 = new TestShardisEnumerator<int>([], "s1");
        var shard2 = new TestShardisEnumerator<int>([], "s2");

        var enumerator = new ShardisAsyncOrderedEnumerator<int, int>(
            [shard1, shard2],
            keySelector: x => x,
            prefetchPerShard: 1);

        // act
        var hasMore = await enumerator.MoveNextAsync();

        // assert
        hasMore.Should().BeFalse();
        enumerator.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task MoveNextAsync_ShouldHandleSingleStream()
    {
        // arrange
        var shard = new TestShardisEnumerator<int>(
            items: [1, 2],
            shardId: "shard1"
        );

        var enumerator = new ShardisAsyncOrderedEnumerator<int, int>(
            [shard],
            keySelector: x => x,
            prefetchPerShard: 1);

        // act
        var results = new List<ShardItem<int>>();
        while (await enumerator.MoveNextAsync())
        {
            results.Add(enumerator.Current);
        }

        // assert
        results.Should().HaveCount(2);
        results.Select(r => r.Item).Should().ContainInOrder(1, 2);
    }

    [Fact]
    public async Task MoveNextAsync_ShouldRespectCancellationToken()
    {
        // arrange
        var shard = new TestShardisEnumerator<int>(
            items: [1, 2],
            shardId: "shard1"
        );

        using var cts = new CancellationTokenSource();
        var enumerator = new ShardisAsyncOrderedEnumerator<int, int>(
            [shard],
            keySelector: x => x,
            prefetchPerShard: 1,
            cancellationToken: cts.Token);

        cts.Cancel();

        // act & assert
        Func<Task> iterate = async () =>
        {
            while (await enumerator.MoveNextAsync())
            {
                _ = enumerator.Current;
            }
        };
        await iterate.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task MoveNextAsync_ShouldMaintainDeterministicOrder_For_DuplicateKeys()
    {
        // arrange
        var shard1 = new TestShardisEnumerator<(int k, string v)>([(1, "a"), (2, "b")], "s1");
        var shard2 = new TestShardisEnumerator<(int k, string v)>([(1, "c"), (2, "d")], "s2");
        var enumerator = new ShardisAsyncOrderedEnumerator<(int k, string v), int>([shard1, shard2], x => x.k, prefetchPerShard: 1);

        // act
        var items = new List<ShardItem<(int k, string v)>>();
        while (await enumerator.MoveNextAsync())
        {
            items.Add(enumerator.Current);
        }

        // assert
        items.Select(i => i.Item.k).Should().ContainInOrder(1, 1, 2, 2);
    }
}