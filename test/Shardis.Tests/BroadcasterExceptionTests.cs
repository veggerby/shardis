using Shardis.Model;
using Shardis.Querying;
using Shardis.Querying.Linq;

namespace Shardis.Tests;

public class BroadcasterExceptionTests
{
    private sealed class ThrowingShard : IShard<string>
    {
        public ShardId ShardId { get; } = new("boom");
        public string CreateSession() => "sess";
        public IShardQueryExecutor<string> QueryExecutor => throw new NotImplementedException();
    }

    [Fact]
    public async Task ExceptionInProducer_ShouldSurface()
    {
        // arrange
        var shard = new ThrowingShard();
        var shards = new List<IShard<string>> { shard };
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards);

        Func<string, IAsyncEnumerable<int>> query = _ => Throwing();

        static async IAsyncEnumerable<int> Throwing()
        {
            await Task.Delay(1);
            yield return 42; // force at least one yield to satisfy iterator contract
            throw new InvalidOperationException("bang");
        }

        // act & assert
        var iterate = async () =>
        {
            await foreach (var _ in broadcaster.QueryAllShardsAsync(query)) { }
        };

        (await iterate.Should().ThrowAsync<AggregateException>()).WithInnerException<InvalidOperationException>().WithMessage("bang");
    }
}