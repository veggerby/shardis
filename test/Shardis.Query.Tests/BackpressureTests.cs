using Shardis.Query.Execution.InMemory;
using Shardis.Query.Internals;

namespace Shardis.Query.Tests;

public sealed class BackpressureTests
{
    [Fact]
    public async Task UnorderedMerge_WithSmallCapacity_StillYieldsAll()
    {
        var shard1 = Enumerable.Range(0, 50).Select(i => (object)i).ToArray();
        var shard2 = Enumerable.Range(50, 50).Select(i => (object)i).ToArray();
        var exec = new InMemoryShardQueryExecutor(new[] { shard1, shard2 }, (streams, ct) => UnorderedMerge.Merge(streams, ct, channelCapacity: 8));
        var q = ShardQuery.For<int>(exec).Where(x => x >= 0);
        var list = await q.ToListAsync();
        list.Count.Should().Be(100);
    }
}