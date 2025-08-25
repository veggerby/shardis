using Shardis.Querying;
using Shardis.Tests.TestHelpers;

namespace Shardis.Tests;

public class ShardisCombinedEnumeratorCancellationTests
{
    [Fact]
    public async Task MoveNextAsync_ShouldRespectCancellation()
    {
        // arrange
    var delays = Enumerable.Repeat(TimeSpan.FromMilliseconds(10), 5);
    var shard = new TestShardisEnumerator<int>([1,2,3,4,5], "s1", delays);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(15);
        var enumerator = new ShardisAsyncCombinedEnumerator<int>([shard], cts.Token);

        // act
        Func<Task> iterate = async () =>
        {
            while (await enumerator.MoveNextAsync()) { _ = enumerator.Current; }
        };

        // assert
        await iterate.Should().ThrowAsync<OperationCanceledException>();
    }
}