using FluentAssertions;

using Shardis.Querying;
using Shardis.Tests.TestHelpers;

namespace Shardis.Tests;

public class ShardisOrderedEnumeratorTests
{
    [Fact]
    public async Task MoveNextAsync_ShouldYieldItemsInOrder()
    {
        // Arrange
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

        // Act
        var results = new List<ShardItem<int>>();
        while (await enumerator.MoveNextAsync())
        {
            results.Add(enumerator.Current);
        }

        // Assert
        results.Should().HaveCount(4);
        results.Select(r => r.Item).Should().ContainInOrder(1, 2, 3, 4);
    }

    [Fact]
    public async Task MoveNextAsync_ShouldReturnFalseWhenAllStreamsAreExhausted()
    {
        // Arrange
        var shard1 = new TestShardisEnumerator<int>([], "s1");
        var shard2 = new TestShardisEnumerator<int>([], "s2");

        var enumerator = new ShardisAsyncOrderedEnumerator<int, int>(
            [shard1, shard2],
            keySelector: x => x);

        // Act
        var hasMore = await enumerator.MoveNextAsync();

        // Assert
        hasMore.Should().BeFalse();
        enumerator.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task MoveNextAsync_ShouldHandleSingleStream()
    {
        // Arrange
        var shard = new TestShardisEnumerator<int>(
            items: [1, 2],
            shardId: "shard1"
        );

        var enumerator = new ShardisAsyncOrderedEnumerator<int, int>(
            [shard],
            keySelector: x => x);

        // Act
        var results = new List<ShardItem<int>>();
        while (await enumerator.MoveNextAsync())
        {
            results.Add(enumerator.Current);
        }

        // Assert
        results.Should().HaveCount(2);
        results.Select(r => r.Item).Should().ContainInOrder(1, 2);
    }

    [Fact]
    public async Task MoveNextAsync_ShouldRespectCancellationToken()
    {
        // Arrange
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

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            while (await enumerator.MoveNextAsync())
            {
                _ = enumerator.Current;
            }
        });
    }
}
