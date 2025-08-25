using Shardis.Model;
using Shardis.Querying;
using Shardis.Querying.Linq;

namespace Shardis.Tests;

public class ShardStreamBroadcasterCancellationTests
{
    private sealed class SlowShard(string id, int count) : IShard<string>
    {
        public ShardId ShardId { get; } = new(id);
        private readonly int _count = count;
        private readonly IShardQueryExecutor<string> _executor = Substitute.For<IShardQueryExecutor<string>>();

        public string CreateSession() => string.Empty;
        public IShardQueryExecutor<string> QueryExecutor => _executor;
        public async IAsyncEnumerable<int> Produce([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            for (int i = 0; i < _count; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(10, ct);
                yield return i;
            }
        }
    }

    [Fact]
    public async Task ShouldCancelRemainingProducers_WhenTokenCancelled()
    {
        // arrange
        var shards = new List<IShard<string>>
        {
            new SlowShard("s1", 50),
            new SlowShard("s2", 50)
        };
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // cancel early
        var collected = new List<int>();

        // act
        try
        {
            await foreach (var item in broadcaster.QueryAllShardsAsync(_ => ((SlowShard)shards[0]).Produce(cts.Token), cts.Token))
            {
                collected.Add(item.Item);
            }
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        // assert
    (collected.Count < 100).Should().BeTrue($"Expected cancellation before producing all items, got {collected.Count}");
    }
}