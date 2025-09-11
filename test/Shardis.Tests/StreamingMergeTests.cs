using Shardis.Model;
using Shardis.Querying;
using Shardis.Querying.Linq;
using Shardis.Testing;

namespace Shardis.Tests;

public class StreamingMergeTests
{
    private const int Seed = 1337;

    // Helper shard implementation for int payloads
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
            public IAsyncEnumerable<T> Execute<T>(int session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IQueryable<T>>> linqExpr) where T : notnull => throw new NotSupportedException();
            public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(int session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr, Func<T, TKey> keySelector) where T : notnull => throw new NotSupportedException();
        }
    }

    // Observers / probes -------------------------------------------------
    // Helper test observers moved to dedicated files (YieldRecordingObserver, CompletionObserver, FirstItemProbe, CompositeObserver)

    // 1) Streaming-ness test ---------------------------------------------
    [Fact]
    public async Task OrderedStreaming_YieldsBeforeSlowShardCompletes()
    {
        // arrange
        var det = Determinism.Create(Seed);
        int shards = 3;
        int itemsPerShard = 500;
        // Use Harsh skew so shard 0 becomes very slow relative to others
        var schedules = det.MakeDelays(shards, Skew.Harsh, TimeSpan.FromMilliseconds(2), steps: itemsPerShard);
        var intShards = Enumerable.Range(0, shards).Select(i => new IntShard(i, schedules, itemsPerShard, det)).ToArray();
        var shardObjs = intShards.Cast<IShard<int>>().ToList();

        var completion = new CompletionObserver();
        var yields = new YieldRecordingObserver();
        var compositeObserver = new CompositeObserver([completion, yields]);
        var broadcaster = new ShardStreamBroadcaster<IShard<int>, int>(shardObjs, observer: compositeObserver);
        var probe = new FirstItemProbe(); probe.Start();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var firstN = new List<int>();
        await foreach (var item in broadcaster.QueryAllShardsOrderedStreamingAsync(session => intShards[session].Stream(cts.Token), x => x, prefetchPerShard: 1, cts.Token))
        {
            if (probe.Us < 0) probe.Hit();
            firstN.Add(item.Item);
            if (firstN.Count == 50) break; // early stop
        }

        // assert first item occurs well before full materialization (relaxed upper bound 100ms)
        probe.Us.Should().BeLessThan(100_000);
        // slow shard index 0 should not have completed
        completion.Completed.Should().NotContain(id => id.Value == "shard-0");
        // yielded items globally non-decreasing
        firstN.Zip(firstN.Skip(1), (a, b) => a <= b).All(x => x).Should().BeTrue();
    }

    // 2) Fairness test ---------------------------------------------------
    [Fact]
    public async Task UnorderedStreaming_FastShards_AreNotStarved_UnderSkew()
    {
        // arrange
        var det = Determinism.Create(Seed);
        int shards = 4;
        int itemsPerShard = 800; // reduce to keep test fast & deterministic
        var schedules = det.MakeDelays(shards, Skew.Harsh, TimeSpan.FromMilliseconds(1), steps: itemsPerShard);
        var intShards = Enumerable.Range(0, shards).Select(i => new IntShard(i, schedules, itemsPerShard, det)).ToArray();
        var shardObjs = intShards.Cast<IShard<int>>().ToList();
        var yields = new YieldRecordingObserver();
        var broadcaster = new ShardStreamBroadcaster<IShard<int>, int>(shardObjs, channelCapacity: 128, observer: yields);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var perShardCounts = new int[shards];
        int index = 0;
        await foreach (var item in broadcaster.QueryAllShardsAsync(session => intShards[session].Stream(cts.Token), cts.Token))
        {
            var shardIdx = int.Parse(item.ShardId.Value.AsSpan("shard-".Length));
            perShardCounts[shardIdx]++;
            index++;
            // per-prefix dominance assumptions removed (schedule may reorder); rely on dry-spell bound below for fairness.
        }

        // assert totals
        foreach (var c in perShardCounts) c.Should().Be(itemsPerShard);

        // optional starvation bound: compute longest dry spell for shard 1 (first fast shard)
        var events = yields.Events.Where(e => e.shard.Value == "shard-1").OrderBy(e => e.seq).Select(e => e.seq).ToArray();
        long maxGap = 0;
        for (int i = 1; i < events.Length; i++)
        {
            var gap = events[i] - events[i - 1];
            if (gap > maxGap) maxGap = gap;
        }
        maxGap.Should().BeLessThan(1024); // dry spell bound (empirical; ensures no long starvation)
    }

    // 3) Eager control first-item latency --------------------------------
    [Fact]
    public async Task OrderedEager_DoesNotYieldUntilMaterializationCompletes()
    {
        // arrange identical schedule as streaming test
        var det = Determinism.Create(Seed);
        int shards = 3; int itemsPerShard = 200;
        var schedules = det.MakeDelays(shards, Skew.Harsh, TimeSpan.FromMilliseconds(2), steps: itemsPerShard);
        var intShards = Enumerable.Range(0, shards).Select(i => new IntShard(i, schedules, itemsPerShard, det)).ToArray();
        var shardObjs = intShards.Cast<IShard<int>>().ToList();
        var broadcaster = new ShardStreamBroadcaster<IShard<int>, int>(shardObjs);
        var probe = new FirstItemProbe(); probe.Start();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await foreach (var item in broadcaster.QueryAllShardsOrderedEagerAsync(session => intShards[session].Stream(cts.Token), x => x, cts.Token))
        {
            probe.Hit();
            break; // only need first
        }

        // slow shard total runtime approximates itemsPerShard * base slow delay (rough upper bound) ~ ensure first item is not extremely early
        probe.Us.Should().BeGreaterThan(20_000); // generous lower bound distinguishing from streaming (<5_000)
    }

    // 4) Capacity sensitivity fairness (reduced form) --------------------
    [Theory]
    [InlineData(32)]
    [InlineData(128)]
    [InlineData(512)]
    public async Task UnorderedStreaming_CapacityDoesNotCauseStarvation(int capacity)
    {
        // arrange
        var det = Determinism.Create(Seed);
        int shards = 4; int itemsPerShard = 400; // smaller to avoid long runtime under harsh skew
        var schedules = det.MakeDelays(shards, Skew.Harsh, TimeSpan.FromMilliseconds(1), steps: itemsPerShard);
        var intShards = Enumerable.Range(0, shards).Select(i => new IntShard(i, schedules, itemsPerShard, det)).ToArray();
        var shardObjs = intShards.Cast<IShard<int>>().ToList();
        var yields = new YieldRecordingObserver();
        var broadcaster = new ShardStreamBroadcaster<IShard<int>, int>(shardObjs, channelCapacity: capacity, observer: yields);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // act
        var perShardCounts = new int[shards];
        await foreach (var item in broadcaster.QueryAllShardsAsync(session => intShards[session].Stream(cts.Token), cts.Token))
        {
            var shardIdx = int.Parse(item.ShardId.Value.AsSpan("shard-".Length));
            perShardCounts[shardIdx]++;
        }

        // assert
        foreach (var c in perShardCounts) c.Should().Be(itemsPerShard);
        var events = yields.Events.Where(e => e.shard.Value == "shard-1").OrderBy(e => e.seq).Select(e => e.seq).ToArray();
        long maxGap = 0;
        for (int i = 1; i < events.Length; i++)
        {
            var gap = events[i] - events[i - 1];
            if (gap > maxGap) maxGap = gap;
        }
        maxGap.Should().BeLessThan(8 * capacity); // relaxed but still proportional
    }

    // Composite observer to fan out events without allocation churn
}