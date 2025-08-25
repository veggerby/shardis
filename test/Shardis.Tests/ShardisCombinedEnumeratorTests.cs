using Shardis.Querying;
using Shardis.Tests.TestHelpers;

namespace Shardis.Tests;

public class ShardisCombinedEnumeratorTests
{
    [Fact]
    public async Task MoveNextAsync_YieldsItemsFromAllEnumerators()
    {
        // arrange
        var shard1 = new TestShardisEnumerator<int>(
            items: [1, 2],
            shardId: "shard1"
        );

        var shard2 = new TestShardisEnumerator<int>(
            items: [3],
            shardId: "shard2"
        );

        var enumerator = new ShardisAsyncCombinedEnumerator<int>(
            [shard1, shard2],
            cancellationToken: CancellationToken.None);

        // act
        var results = new List<ShardItem<int>>();
        while (await enumerator.MoveNextAsync())
        {
            results.Add(enumerator.Current);
        }

        // assert
        results.Should().HaveCount(3);
        results.Should().ContainSingle(item => item.ShardId.Value == "shard1" && item.Item == 1);
        results.Should().ContainSingle(item => item.ShardId.Value == "shard1" && item.Item == 2);
        results.Should().ContainSingle(item => item.ShardId.Value == "shard2" && item.Item == 3);
    }

    [Fact]
    public async Task MoveNextAsync_CompletesWhenAllEnumeratorsAreExhausted()
    {
        // arrange
        var shard1 = new TestShardisEnumerator<int>([], "shard1");
        var shard2 = new TestShardisEnumerator<int>([], "shard2");

        var enumerator = new ShardisAsyncCombinedEnumerator<int>(
            [shard1, shard2],
            cancellationToken: CancellationToken.None);

        // act
        var hasMore = await enumerator.MoveNextAsync();

        // assert
        hasMore.Should().BeFalse();
        enumerator.IsComplete.Should().BeTrue();
    }
}