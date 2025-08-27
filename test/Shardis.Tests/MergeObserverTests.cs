using System.Collections.Concurrent;

using Shardis.Model;
using Shardis.Querying;
using Shardis.Querying.Linq;

using Xunit;

namespace Shardis.Tests;

public class MergeObserverTests
{
    private sealed class TestShard(string id, int count, int delayMs) : IShard<string>
    {
        public ShardId ShardId { get; } = new(id);
        public string CreateSession() => id;
        public IShardQueryExecutor<string> QueryExecutor => new DummyExecutor();
        public async IAsyncEnumerable<int> Stream()
        {
            for (int i = 0; i < count; i++)
            {
                if (delayMs > 0) await Task.Delay(delayMs);
                yield return i;
            }
        }

        private sealed class DummyExecutor : Shardis.Querying.Linq.IShardQueryExecutor<string>
        {
            public IAsyncEnumerable<T> Execute<T>(string session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IQueryable<T>>> linqExpr) where T : notnull => throw new NotSupportedException();
            public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(string session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr, Func<T, TKey> keySelector) where T : notnull => throw new NotSupportedException();
        }
    }

    private sealed class RecordingObserver : IMergeObserver
    {
        public ConcurrentBag<ShardId> ItemShards = new();
        public ConcurrentBag<ShardId> Completed = new();
        public int BackpressureStarts; public int BackpressureStops; public ConcurrentBag<int> HeapSizes = new();
        public void OnItemYielded(ShardId shardId) => ItemShards.Add(shardId);
        public void OnShardCompleted(ShardId shardId) => Completed.Add(shardId);
        public void OnBackpressureWaitStart() => Interlocked.Increment(ref BackpressureStarts);
        public void OnBackpressureWaitStop() => Interlocked.Increment(ref BackpressureStops);
        public void OnHeapSizeSample(int size) => HeapSizes.Add(size);
    }

    [Fact]
    public async Task OrderedStreaming_ReportsItems_Completions_HeapSamples()
    {
        // arrange
        var shards = new IShard<string>[]
        {
            new TestShard("A", 5, 0),
            new TestShard("B", 5, 1)
        };
        var observer = new RecordingObserver();
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards, observer: observer);

        IAsyncEnumerable<int> Query(string session) => ((TestShard)shards.First(s => s.CreateSession() == session)).Stream();

        // act
        var results = new List<ShardItem<int>>();
        await foreach (var item in broadcaster.QueryAllShardsOrderedStreamingAsync(Query, x => x, prefetchPerShard: 1))
        {
            results.Add(item);
        }

        // assert
        Assert.Equal(10, results.Count);
        Assert.Equal(10, observer.ItemShards.Count);
        Assert.Equal(2, observer.Completed.Distinct().Count());
        Assert.True(observer.HeapSizes.Count > 0);
        Assert.All(observer.HeapSizes, s => Assert.InRange(s, 0, 2));
    }

    [Fact]
    public async Task Unordered_Backpressure_Waits_Are_Observed()
    {
        // arrange (tiny channel capacity to force waits)
        var shards = new IShard<string>[]
        {
            new TestShard("A", 50, 0),
            new TestShard("B", 50, 0)
        };
        var observer = new RecordingObserver();
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards, channelCapacity: 1, observer: observer);
        IAsyncEnumerable<int> Query(string session) => ((TestShard)shards.First(s => s.CreateSession() == session)).Stream();

        // act
        int count = 0;
        await foreach (var _ in broadcaster.QueryAllShardsAsync(Query)) { count++; }

        // assert
        Assert.Equal(100, count);
        Assert.True(observer.BackpressureStarts > 0);
        Assert.Equal(observer.BackpressureStarts, observer.BackpressureStops);
    }

    private sealed class ThrowingObserver : IMergeObserver
    {
        public int Throws;
        public void OnItemYielded(ShardId shardId) { Throws++; throw new InvalidOperationException("boom"); }
        public void OnShardCompleted(ShardId shardId) { throw new InvalidOperationException("boom"); }
        public void OnBackpressureWaitStart() { throw new InvalidOperationException("boom"); }
        public void OnBackpressureWaitStop() { throw new InvalidOperationException("boom"); }
        public void OnHeapSizeSample(int size) { throw new InvalidOperationException("boom"); }
    }

    [Fact]
    public async Task Observer_Exceptions_Are_Swallowed()
    {
        var shards = new IShard<string>[] { new TestShard("A", 5, 0) };
        var throwing = new ThrowingObserver();
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards, observer: throwing);
        IAsyncEnumerable<int> Query(string session) => ((TestShard)shards.First()).Stream();
        var list = new List<int>();
        await foreach (var item in broadcaster.QueryAllShardsOrderedStreamingAsync(Query, x => x)) { list.Add(item.Item); }
        Assert.Equal(5, list.Count);
        Assert.True(throwing.Throws >= 5); // item yielded exceptions were triggered
    }
}