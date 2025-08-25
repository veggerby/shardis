using AwesomeAssertions;

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
            cancellationToken: CancellationToken.None);

        // act
        var results = new List<ShardItem<int>>();
        while (await enumerator.MoveNextAsync())
        {
            results.Add(enumerator.Current);
        }

        // assert
        results.ShouldHaveCount(4);
        results.Select(r => r.Item).ShouldContainInOrder(1, 2, 3, 4);
    }

    [Fact]
    public async Task MoveNextAsync_ShouldReturnFalseWhenAllStreamsAreExhausted()
    {
        // arrange
        var shard1 = new TestShardisEnumerator<int>([], "s1");
        var shard2 = new TestShardisEnumerator<int>([], "s2");

        var enumerator = new ShardisAsyncOrderedEnumerator<int, int>(
            [shard1, shard2],
            keySelector: x => x);

        // act
        var hasMore = await enumerator.MoveNextAsync();

        // assert
        hasMore.ShouldBeFalse();
        enumerator.IsComplete.ShouldBeTrue();
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
            keySelector: x => x);

        // act
        var results = new List<ShardItem<int>>();
        while (await enumerator.MoveNextAsync())
        {
            results.Add(enumerator.Current);
        }

        // assert
        results.ShouldHaveCount(2);
        results.Select(r => r.Item).ShouldContainInOrder(1, 2);
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
        await iterate.ShouldThrowAsync<OperationCanceledException>();
    }
}