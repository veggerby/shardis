using Shardis.Model;
using Shardis.Querying;
using Shardis.Querying.Linq;
using Shardis.Testing;

namespace Shardis.Tests;

[Trait("category", "metrics")]
public class MetricsObserverTests
{
    private const int Seed = 1337;

    private sealed class CountingObserver : IMergeObserver
    {
        public long Items, HeapSamples, WaitStarts, WaitStops, Completed, Stopped;
        public void OnItemYielded(ShardId _) => Interlocked.Increment(ref Items);
        public void OnHeapSizeSample(int _) => Interlocked.Increment(ref HeapSamples);
        public void OnBackpressureWaitStart() => Interlocked.Increment(ref WaitStarts);
        public void OnBackpressureWaitStop() => Interlocked.Increment(ref WaitStops);
        public void OnShardCompleted(ShardId _) => Interlocked.Increment(ref Completed);
        public void OnShardStopped(ShardId _, ShardStopReason __) => Interlocked.Increment(ref Stopped);
    }

    private sealed class IntShard(int index, TimeSpan[][] schedules, int items, Determinism det) : IShard<int>
    {
        private readonly Determinism _det = det;

        public ShardId ShardId { get; } = new($"shard-{index}");
        public int CreateSession() => index;
        public IShardQueryExecutor<int> QueryExecutor => DummyExecutor.Instance;
        public async IAsyncEnumerable<int> Stream([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            for (int i = 0; i < items; i++)
            {
                if (ct.IsCancellationRequested) yield break;
                await Determinism.DelayForShardAsync(schedules, index, i, ct).ConfigureAwait(false);
                yield return i;
            }
        }
        private sealed class DummyExecutor : IShardQueryExecutor<int>
        {
            public static readonly DummyExecutor Instance = new();
            public IAsyncEnumerable<T> Execute<T>(int session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IQueryable<T>>> _) where T : notnull => throw new NotSupportedException();
            public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(int session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> _, Func<T, TKey> __) where T : notnull => throw new NotSupportedException();
        }
    }

    [Fact]
    public async Task Unordered_ObserverCounts_AreCoherent()
    {
        // arrange
        var det = Determinism.Create(Seed);
        int shards = 3, items = 150;
        var schedules = det.MakeDelays(shards, Skew.Mild, TimeSpan.FromMilliseconds(1), steps: items);
        var shardObjs = Enumerable.Range(0, shards).Select(i => new IntShard(i, schedules, items, det)).Cast<IShard<int>>().ToArray();
        var obs = new CountingObserver();
        var bc = new ShardStreamBroadcaster<IShard<int>, int>(shardObjs, channelCapacity: 64, observer: obs);

        // act
        int total = 0;
        await foreach (var it in bc.QueryAllShardsAsync(s => ((IntShard)shardObjs[s]).Stream()))
        {
            total++;
        }

        // assert
        total.Should().Be(shards * items);
        Interlocked.Read(ref obs.Items).Should().Be(total);
        Interlocked.Read(ref obs.WaitStarts).Should().Be(Interlocked.Read(ref obs.WaitStops));
        Interlocked.Read(ref obs.Completed).Should().Be(shards);
        Interlocked.Read(ref obs.Stopped).Should().BeGreaterThanOrEqualTo(shards);
    }

    [Fact]
    public async Task Unordered_Unbounded_NoBackpressureWaits()
    {
        // arrange
        var det = Determinism.Create(Seed);
        int shards = 2, items = 50;
        var schedules = det.MakeDelays(shards, Skew.None, TimeSpan.FromMilliseconds(1), steps: items);
        var shardObjs = Enumerable.Range(0, shards).Select(i => new IntShard(i, schedules, items, det)).Cast<IShard<int>>().ToArray();
        var obs = new CountingObserver();
        // unbounded: no capacity passed
        var bc = new ShardStreamBroadcaster<IShard<int>, int>(shardObjs, observer: obs);

        // act
        int total = 0;
        await foreach (var it in bc.QueryAllShardsAsync(s => ((IntShard)shardObjs[s]).Stream()))
        {
            total++;
        }

        // assert
        total.Should().Be(shards * items);
        Interlocked.Read(ref obs.WaitStarts).Should().Be(0);
        Interlocked.Read(ref obs.WaitStops).Should().Be(0);
    }

    [Fact]
    public async Task OrderedStreaming_HeapSamples_And_Items_ArePositive_AndLifecycleConsistent()
    {
        // arrange
        var det = Determinism.Create(Seed);
        int shards = 4, items = 200;
        var schedules = det.MakeDelays(shards, Skew.Mild, TimeSpan.FromMilliseconds(1), steps: items);
        var shardObjs = Enumerable.Range(0, shards).Select(i => new IntShard(i, schedules, items, det)).Cast<IShard<int>>().ToArray();
        var obs = new CountingObserver();
        var bc = new ShardStreamBroadcaster<IShard<int>, int>(shardObjs, observer: obs, heapSampleEvery: 1);

        // act
        int total = 0;
        await foreach (var it in bc.QueryAllShardsOrderedStreamingAsync(s => ((IntShard)shardObjs[s]).Stream(), x => x, prefetchPerShard: 2))
        {
            total++;
        }

        // assert
        total.Should().Be(shards * items);
        Interlocked.Read(ref obs.Items).Should().Be(total);
        Interlocked.Read(ref obs.HeapSamples).Should().BeGreaterThan(0);
        Interlocked.Read(ref obs.Completed).Should().Be(shards);
        Interlocked.Read(ref obs.Stopped).Should().BeGreaterThanOrEqualTo(shards);
        // ordered streaming with default (unbounded) capacity should emit no backpressure waits
        Interlocked.Read(ref obs.WaitStarts).Should().Be(0);
        Interlocked.Read(ref obs.WaitStops).Should().Be(0);
    }
}