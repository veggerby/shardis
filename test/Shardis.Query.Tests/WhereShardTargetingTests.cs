using Shardis.Query.InMemory.Execution;

namespace Shardis.Query.Tests;

public sealed class WhereShardTargetingTests
{
    [Fact]
    public async Task SingleShard_TargetsOnlyOne()
    {
        // arrange
        var shards = new List<IEnumerable<object>>
        {
            new object[] { 1, 2, 3 },
            new object[] { 4, 5 },
            new object[] { 6 }
        };
        var observer = new RecordingObserver();
        var exec = new InMemoryShardQueryExecutor(shards, (streams, ct) => Shardis.Query.Internals.UnorderedMerge.Merge(streams, ct), observer);
        var q = ShardQuery.For<int>(exec).WhereShard(new Shardis.Model.ShardId("1"));

        // act
        var list = await q.ToListAsync();

        // assert
        list.Should().OnlyContain(i => ((int)i) >= 4 && ((int)i) <= 5);
        observer.ShardStarts.Should().Be(1);
    }

    private sealed class RecordingObserver : Shardis.Query.Diagnostics.IQueryMetricsObserver
    {
        public int ShardStarts; public int ShardStops; public int ItemsProduced; public bool Completed; public bool Canceled;
        public void OnShardStart(int shardId) => Interlocked.Increment(ref ShardStarts);
        public void OnItemsProduced(int shardId, int count) => Interlocked.Add(ref ItemsProduced, count);
        public void OnShardStop(int shardId) => Interlocked.Increment(ref ShardStops);
        public void OnCompleted() => Completed = true;
        public void OnCanceled() => Canceled = true;
    }
}