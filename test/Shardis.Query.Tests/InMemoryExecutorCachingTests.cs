using Shardis.Query.InMemory.Execution;
using Shardis.Query.Internals;

namespace Shardis.Query.Tests;

public sealed class InMemoryExecutorCachingTests
{
    [Fact]
    public async Task CompilePipeline_OncePerDistinctQueryModel()
    {
        // arrange
        var shard1 = new object[] { new Person { Id = 1, Age = 50 }, new Person { Id = 2, Age = 20 } };
        var shard2 = new object[] { new Person { Id = 3, Age = 70 } };
        var exec = new InMemoryShardQueryExecutor(new[] { shard1, shard2 }, (streams, ct) => UnorderedMerge.Merge(streams, ct));
        var before = InMemoryShardQueryExecutor.CompileCount;
        var q1 = ShardQuery.For<Person>(exec).Where(p => p.Age > 30).Select(p => p.Age);
        var q2 = ShardQuery.For<Person>(exec).Where(p => p.Age > 30).Select(p => p.Age); // structurally identical new model

        // act
        _ = await q1.ToListAsync();
        var afterFirst = InMemoryShardQueryExecutor.CompileCount;
        _ = await q2.ToListAsync();
        var afterSecond = InMemoryShardQueryExecutor.CompileCount;

        // assert
        // Second identical model should NOT trigger an additional compilation (cache hit).
        (afterSecond - afterFirst).Should().Be(0);
    }

    [Fact]
    public async Task Metrics_Observer_ReceivesLifecycle_InMemory()
    {
        // arrange
        var shard1 = new object[] { new Person { Id = 1, Age = 50 }, new Person { Id = 2, Age = 20 } };
        var shard2 = new object[] { new Person { Id = 3, Age = 70 } };
        var obs = new RecordingObserver();
        var exec = new InMemoryShardQueryExecutor(new[] { shard1, shard2 }, (s, ct) => UnorderedMerge.Merge(s, ct), obs);
        var q = ShardQuery.For<Person>(exec).Where(p => p.Age > 30);

        // act
        var list = await q.ToListAsync();

        // assert
        list.Count.Should().Be(2);
        obs.ShardStarts.Should().Be(2);
        obs.ItemsProduced.Should().BeGreaterThan(0);
        obs.Completed.Should().BeTrue();
        obs.Canceled.Should().BeFalse();
    }

    private sealed class RecordingObserver : Diagnostics.IQueryMetricsObserver
    {
        public int ShardStarts; public int ItemsProduced; public int ShardStops; public bool Completed; public bool Canceled;
        public void OnShardStart(int shardId) => Interlocked.Increment(ref ShardStarts);
        public void OnItemsProduced(int shardId, int count) => Interlocked.Add(ref ItemsProduced, count);
        public void OnShardStop(int shardId) => Interlocked.Increment(ref ShardStops);
        public void OnCompleted() => Completed = true;
        public void OnCanceled() => Canceled = true;
    }

    private sealed class Person { public int Id { get; set; } public int Age { get; set; } }
}