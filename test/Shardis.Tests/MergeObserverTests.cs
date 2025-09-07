using System.Collections.Concurrent;

using Shardis.Model;
using Shardis.Querying;
using Shardis.Querying.Linq;

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

        private sealed class DummyExecutor : IShardQueryExecutor<string>
        {
            public IAsyncEnumerable<T> Execute<T>(string session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IQueryable<T>>> linqExpr) where T : notnull => throw new NotSupportedException();
            public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(string session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr, Func<T, TKey> keySelector) where T : notnull => throw new NotSupportedException();
        }
    }

    private sealed class RecordingObserver : IMergeObserver
    {
        public ConcurrentBag<ShardId> ItemShards = [];
        public ConcurrentBag<ShardId> Completed = [];
        public ConcurrentBag<(ShardId, ShardStopReason)> Stopped = [];
        public ConcurrentQueue<(string Event, ShardId Id, ShardStopReason? Reason)> Sequence = new();
        public int BackpressureStarts; public int BackpressureStops; public ConcurrentBag<int> HeapSizes = [];
        public void OnItemYielded(ShardId shardId) { ItemShards.Add(shardId); }
        public void OnShardCompleted(ShardId shardId) { Completed.Add(shardId); Sequence.Enqueue(("Completed", shardId, null)); }
        public void OnShardStopped(ShardId shardId, ShardStopReason reason) { Stopped.Add((shardId, reason)); Sequence.Enqueue(("Stopped", shardId, reason)); }
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
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards, observer: observer, heapSampleEvery: 1);

        IAsyncEnumerable<int> Query(string session) => ((TestShard)shards.First(s => s.CreateSession() == session)).Stream();

        // act
        var results = new List<ShardItem<int>>();
        await foreach (var item in broadcaster.QueryAllShardsOrderedStreamingAsync(Query, x => x, prefetchPerShard: 1))
        {
            results.Add(item);
        }

        // assert
        results.Count.Should().Be(10);
        observer.ItemShards.Count.Should().Be(10);
        observer.Completed.Distinct().Count().Should().Be(2);
        observer.HeapSizes.Count.Should().BeGreaterThan(0);
        observer.HeapSizes.Should().OnlyContain(s => s >= 0 && s <= 2);
        observer.Stopped.Count.Should().Be(2);
        observer.Stopped.Should().OnlyContain(s => s.Item2 == ShardStopReason.Completed);
        // ordering: Completed must precede Stopped for each shard
        foreach (var shardId in observer.Completed)
        {
            var seqEvents = observer.Sequence.Where(e => e.Id.Equals(shardId)).ToList();
            seqEvents[0].Event.Should().Be("Completed");
            seqEvents[1].Event.Should().Be("Stopped");
            seqEvents[1].Reason.Should().Be(ShardStopReason.Completed);
        }
    }

    [Fact]
    public async Task OrderedStreaming_SingleFire_Stop_And_Order()
    {
        var shards = new IShard<string>[] { new TestShard("A", 3, 0) };
        var observer = new RecordingObserver();
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards, observer: observer);
        IAsyncEnumerable<int> Query(string session) => ((TestShard)shards.First()).Stream();
        await foreach (var _ in broadcaster.QueryAllShardsOrderedStreamingAsync(Query, x => x)) { }
        observer.Completed.Should().HaveCount(1);
        observer.Stopped.Should().HaveCount(1);
        var seq = observer.Sequence.Where(e => e.Id.Equals(observer.Completed.First())).ToList();
        seq.Count.Should().Be(2);
        seq[0].Event.Should().Be("Completed");
        seq[1].Event.Should().Be("Stopped");
        seq[1].Reason.Should().Be(ShardStopReason.Completed);
    }

    [Fact]
    public async Task Unordered_StartupFault_Reports_Faulted_Stop()
    {
        var shards = new IShard<string>[] { new TestShard("A", 5, 0) };
        var observer = new RecordingObserver();
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards, observer: observer);
        static IAsyncEnumerable<int> Faulting(string _) => throw new InvalidOperationException("boom-start");
        Exception? ex = null;
        try
        {
            await foreach (var _ in broadcaster.QueryAllShardsAsync(Faulting)) { }
        }
        catch (Exception e) { ex = e; }
        ex.Should().NotBeNull();
        // wait briefly for finalizer
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (observer.Stopped.Count == 0 && sw.ElapsedMilliseconds < 500) { await Task.Delay(10); }
        observer.Stopped.Should().HaveCount(1);
        observer.Stopped.First().Item2.Should().Be(ShardStopReason.Faulted);
    }

    [Fact]
    public async Task OrderedStreaming_HeapSampling_Throttle()
    {
        var shards = new IShard<string>[] { new TestShard("A", 50, 0), new TestShard("B", 50, 0) };
        var observer = new RecordingObserver();
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards, observer: observer, heapSampleEvery: 10);
        IAsyncEnumerable<int> Query(string session) => ((TestShard)shards.First(s => s.CreateSession() == session)).Stream();
        int count = 0;
        await foreach (var _ in broadcaster.QueryAllShardsOrderedStreamingAsync(Query, x => x)) { count++; }
        count.Should().Be(100);
        // With heapSampleEvery=10 and 2 shards prefetch=1, sample count should be <= ~count/5 (loose upper bound)
        observer.HeapSizes.Count.Should().BeGreaterThan(0);
        observer.HeapSizes.Count.Should().BeLessThanOrEqualTo(30, $"Heap sample count too high: {observer.HeapSizes.Count}");
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
        count.Should().Be(100);
        observer.BackpressureStarts.Should().BeGreaterThan(0);
        observer.BackpressureStops.Should().Be(observer.BackpressureStarts);
    }

    private sealed class ThrowingObserver : IMergeObserver
    {
        public int Throws;
        public void OnItemYielded(ShardId shardId) { Throws++; throw new InvalidOperationException("boom"); }
        public void OnShardCompleted(ShardId shardId) { throw new InvalidOperationException("boom"); }
        public void OnShardStopped(ShardId shardId, ShardStopReason reason) { }
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
        list.Count.Should().Be(5);
        throwing.Throws.Should().BeGreaterThanOrEqualTo(5); // item yielded exceptions were triggered
    }

    [Fact]
    public async Task Unordered_Cancel_Reports_Stopped_With_Canceled()
    {
        var shards = new IShard<string>[] { new TestShard("A", 10_000, 0) };
        var observer = new RecordingObserver();
        // Use bounded channel to keep producer from fully completing before cancellation but allow cancellation to interrupt WriteAsync.
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards, channelCapacity: 32, observer: observer);
        using var cts = new CancellationTokenSource();
        IAsyncEnumerable<int> Query(string session) => ((TestShard)shards.First()).Stream();
        int seen = 0;
        try
        {
            await foreach (var _ in broadcaster.QueryAllShardsAsync(Query, cts.Token))
            {
                if (++seen > 5) { cts.Cancel(); }
            }
        }
        catch (Exception) { /* ignore */ }
        // Enumeration may exit before shard task finalizer runs; wait briefly for stop notification.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (observer.Stopped.Count == 0 && sw.ElapsedMilliseconds < 2000)
        {
            await Task.Delay(20);
        }
        observer.Stopped.Should().HaveCount(1); // ensure exactly one stop recorded
        var stop = observer.Stopped.First();
        (stop.Item2 is ShardStopReason.Canceled or ShardStopReason.Completed).Should().BeTrue($"Unexpected stop reason {stop.Item2}");
    }

    private sealed class InfiniteShard(string id) : IShard<string>
    {
        public ShardId ShardId { get; } = new(id);
        public string CreateSession() => id;
        public IShardQueryExecutor<string> QueryExecutor => throw new NotSupportedException();
        public async IAsyncEnumerable<int> Stream()
        {
            var i = 0;
            while (true)
            {
                yield return i++;
                await Task.Delay(10); // slow enough to let cancellation trigger mid-stream
            }
        }
    }

    [Fact]
    public async Task Unordered_Deterministic_Cancel_Reports_Canceled_Stop()
    {
        var shards = new IShard<string>[] { new InfiniteShard("INF") };
        var observer = new RecordingObserver();
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards, channelCapacity: 16, observer: observer);
        using var cts = new CancellationTokenSource();
        IAsyncEnumerable<int> Query(string session) => ((InfiniteShard)shards.First()).Stream();
        int seen = 0;
        Exception? ex = null;
        try
        {
            await foreach (var _ in broadcaster.QueryAllShardsAsync(Query, cts.Token))
            {
                if (++seen == 3) { cts.Cancel(); }
            }
        }
        catch (Exception e) { ex = e; }
        // Expect cancellation propagated
        ex.Should().NotBeNull();
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        while (observer.Stopped.Count == 0 && sw2.ElapsedMilliseconds < 1000) { await Task.Delay(10); }
        observer.Stopped.Should().HaveCount(1);
        observer.Stopped.First().Item2.Should().Be(ShardStopReason.Canceled);
    }

    private sealed class FaultingShard(string id) : IShard<string>
    {
        public ShardId ShardId { get; } = new(id);
        public string CreateSession() => id;
        public IShardQueryExecutor<string> QueryExecutor => new DummyExecutor();
        public async IAsyncEnumerable<int> Stream()
        {
            yield return 1;
            await Task.Yield();
            throw new InvalidOperationException("boom");
        }
        private sealed class DummyExecutor : IShardQueryExecutor<string>
        {
            public IAsyncEnumerable<T> Execute<T>(string session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IQueryable<T>>> linqExpr) where T : notnull => throw new NotSupportedException();
            public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(string session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr, Func<T, TKey> keySelector) where T : notnull => throw new NotSupportedException();
        }
    }

    [Fact]
    public async Task Unordered_Fault_Reports_Stopped_With_Faulted()
    {
        var shards = new IShard<string>[] { new FaultingShard("F") };
        var observer = new RecordingObserver();
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards, channelCapacity: 4, observer: observer);
        IAsyncEnumerable<int> Query(string session) => ((FaultingShard)shards.First()).Stream();
        Exception? captured = null;
        try
        {
            await foreach (var _ in broadcaster.QueryAllShardsAsync(Query)) { }
        }
        catch (Exception ex) { captured = ex; }
        captured.Should().NotBeNull();
        var flat = captured is AggregateException agg ? agg.Flatten().InnerExceptions.First() : captured;
        flat!.Message.Should().Be("boom");
        observer.Stopped.Should().HaveCount(1);
        observer.Stopped.First().Item2.Should().Be(ShardStopReason.Faulted);
    }
}