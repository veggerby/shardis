using System.Diagnostics;

using Shardis.Model;
using Shardis.Querying;
using Shardis.Querying.Linq;
using Shardis.Testing;
using Shardis.Tests.TestInfra;

namespace Shardis.Tests;

[Trait("category", "cancellation")]
public class CancellationAndLeakTests
{
    private const int Seed = 1337;

    private sealed class IntShard(int index, TimeSpan[][] schedules, int items, Determinism det) : IShard<int>
    {
        public ShardId ShardId { get; } = new($"shard-{index}");
        public int CreateSession() => index;
        public IShardQueryExecutor<int> QueryExecutor => DummyExecutor.Instance;
        public async IAsyncEnumerable<int> Stream([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            for (int i = 0; i < items; i++)
            {
                if (ct.IsCancellationRequested) yield break;
                await det.DelayForShardAsync(schedules, index, i, ct).ConfigureAwait(false);
                yield return i;
            }
        }
        private sealed class DummyExecutor : IShardQueryExecutor<int>
        {
            public static readonly DummyExecutor Instance = new();
            public IAsyncEnumerable<T> Execute<T>(int session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IQueryable<T>>> expr) where T : notnull => throw new NotSupportedException();
            public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(int session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> expr, Func<T, TKey> key) where T : notnull => throw new NotSupportedException();
        }
    }

    [Fact]
    public async Task Unordered_Cancel_Early_NoDeadlock_NoLeak()
    {
        var leak = new LeakProbe();
        async Task RunAsync()
        {
            var det = Determinism.Create(Seed);
            int shards = 4, items = 400;
            var schedules = det.MakeDelays(shards, Skew.Mild, TimeSpan.FromMilliseconds(1), steps: items);
            var shardObjs = Enumerable.Range(0, shards).Select(i => new IntShard(i, schedules, items, det)).Cast<IShard<int>>().ToArray();

            var broadcaster = new ShardStreamBroadcaster<IShard<int>, int>(shardObjs, channelCapacity: 32);
            leak.Track(broadcaster);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            int yielded = 0;
            var sw = Stopwatch.StartNew();
            await foreach (var it in broadcaster.QueryAllShardsAsync<int>(s => ((IntShard)shardObjs[s]).Stream(cts.Token), cts.Token))
            {
                yielded++;
                if (yielded >= 50) { cts.Cancel(); break; }
            }
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        }
        await RunAsync();
        const int MaxCycles = 3;
        bool collected = false;
        for (int i = 0; i < MaxCycles && !(collected = leak.AllCollected()); i++)
        {
            await Task.Delay(50);
            leak.ForceGC();
        }
        collected.Should().BeTrue("Broadcaster should be eligible for GC after cancellation (unordered early cancel)");
    }

    [Fact]
    public async Task OrderedStreaming_Cancel_Midway_DisposesEnumerators_NoLeak()
    {
        var leak = new LeakProbe();
        async Task RunAsync()
        {
            var det = Determinism.Create(Seed);
            int shards = 3, items = 500;
            var schedules = det.MakeDelays(shards, Skew.Harsh, TimeSpan.FromMilliseconds(1), steps: items);
            var shardObjs = Enumerable.Range(0, shards).Select(i => new IntShard(i, schedules, items, det)).Cast<IShard<int>>().ToArray();
            var broadcaster = new ShardStreamBroadcaster<IShard<int>, int>(shardObjs);
            leak.Track(broadcaster);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            int yielded = 0;
            await foreach (var it in broadcaster.QueryAllShardsOrderedStreamingAsync<int, int>(s => ((IntShard)shardObjs[s]).Stream(cts.Token), x => x, prefetchPerShard: 1, cts.Token))
            {
                yielded++;
                if (yielded >= 80) { cts.Cancel(); break; }
            }
        }
        await RunAsync();
        const int MaxCycles = 3;
        bool collected = false;
        for (int i = 0; i < MaxCycles && !(collected = leak.AllCollected()); i++)
        {
            await Task.Delay(50);
            leak.ForceGC();
        }
        collected.Should().BeTrue("Broadcaster should be eligible for GC after mid-way cancellation (ordered streaming)");
    }

    [Fact]
    public async Task Unordered_SmallCapacity_NoDeadlock_OnEarlyCancel()
    {
        var det = Determinism.Create(Seed);
        int shards = 4, items = 300;
        var schedules = det.MakeDelays(shards, Skew.Harsh, TimeSpan.FromMilliseconds(1), steps: items);
        var shardObjs = Enumerable.Range(0, shards).Select(i => new IntShard(i, schedules, items, det)).Cast<IShard<int>>().ToArray();

        var broadcaster = new ShardStreamBroadcaster<IShard<int>, int>(shardObjs, channelCapacity: 16);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var task = Task.Run(async () =>
        {
            await foreach (var _ in broadcaster.QueryAllShardsAsync<int>(s => ((IntShard)shardObjs[s]).Stream(cts.Token), cts.Token))
            {
                cts.Cancel();
                break;
            }
        });

        await task.WaitAsync(TimeSpan.FromSeconds(1));
    }
}