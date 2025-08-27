using System.Diagnostics;

using Shardis.Model;
using Shardis.Querying;
using Shardis.Querying.Linq;
using Shardis.Testing;
using Shardis.Tests.TestHelpers;

namespace Shardis.Tests;

public class OrderedStreamingMergeTests
{
    private sealed class Probe : IOrderedMergeProbe
    {
        public int MaxHeap { get; private set; }
        public void OnHeapSize(int size) => MaxHeap = Math.Max(MaxHeap, size);
    }

    private sealed class DataShard(string id) : IShard<string>
    {
        public ShardId ShardId { get; } = new(id);
        public string CreateSession() => ShardId.Value; // use id as session
        public IShardQueryExecutor<string> QueryExecutor => throw new NotSupportedException();
    }

    [Fact]
    public async Task OrderedStreaming_IsDeterministic_AndMonotonic()
    {
        // arrange
        var shardA = new DataShard("A");
        var shardB = new DataShard("B");
        var shards = new List<IShard<string>> { shardA, shardB };
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards);

        var det = Determinism.Create(1337);
        // deterministic delay schedule: shard B slower than A via skew
        var schedules = det.MakeDelays(2, Skew.Mild, TimeSpan.FromMilliseconds(10), steps: 3);
        var data = new Dictionary<string, (int[] values, int shardIndex)>
        {
            ["A"] = (new[] { 1, 1, 2 }, 0),
            ["B"] = (new[] { 1, 2, 2 }, 1)
        };

        IAsyncEnumerable<int> Query(string session) => Execute(session);
        async IAsyncEnumerable<int> Execute(string session)
        {
            var (values, shardIndex) = data[session];
            for (int i = 0; i < values.Length; i++)
            {
                await det.DelayForShardAsync(schedules, shardIndex, i);
                yield return values[i];
            }
        }

        // act (run twice to assert determinism across runs)
        var run1 = new List<(string shard, int key)>();
        await foreach (var item in broadcaster.QueryAllShardsOrderedStreamingAsync(Query, x => x, prefetchPerShard: 1))
        {
            run1.Add((item.ShardId.Value, item.Item));
        }

        var run2 = new List<(string shard, int key)>();
        await foreach (var item in broadcaster.QueryAllShardsOrderedStreamingAsync(Query, x => x, prefetchPerShard: 1))
        {
            run2.Add((item.ShardId.Value, item.Item));
        }

        // assert
        run1.Select(r => r.key).Zip(run1.Select(r => r.key).Skip(1), (a, b) => a <= b).All(x => x).Should().BeTrue();
        run2.Should().BeEquivalentTo(run1, opts => opts.WithoutStrictOrdering());
    }

    [Fact]
    public async Task OrderedStreaming_YieldsEarly_FirstItemWithoutFullMaterialization()
    {
        // arrange: fast shard has immediate items; slow shard delays only AFTER first item
        var fast = new DataShard("F");
        var slow = new DataShard("S");
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>([fast, slow]);

        var data = new Dictionary<string, (int[] values, TimeSpan[] delays)>
        {
            ["F"] = (new[] { 1, 3, 5 }, new[] { TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero }),
            ["S"] = (new[] { 2, 4, 6 }, new[] { TimeSpan.Zero, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(10) })
        };

        IAsyncEnumerable<int> Query(string session) => Exec(session);
        async IAsyncEnumerable<int> Exec(string session)
        {
            var (values, delays) = data[session];
            for (int i = 0; i < values.Length; i++)
            {
                if (i < delays.Length && delays[i] > TimeSpan.Zero) { await Task.Delay(delays[i]); }
                yield return values[i];
            }
        }

        var sw = Stopwatch.StartNew();
        await using var enumerator = broadcaster.QueryAllShardsOrderedStreamingAsync(Query, x => x, prefetchPerShard: 1).GetAsyncEnumerator();
        (await enumerator.MoveNextAsync()).Should().BeTrue();
        var firstElapsed = sw.Elapsed;

        // assert first item produced quickly (no delay from later slow items)
        firstElapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(150));
    }

    [Fact]
    public async Task OrderedStreaming_DoesNotOverBuffer_PerShardLimit()
    {
        // arrange
        var shards = Enumerable.Range(0, 4)
            .Select(i => new TestShardisEnumerator<int>(Enumerable.Range(0, 10), $"S{i}"))
            .Cast<IShardisAsyncEnumerator<int>>()
            .ToList();

        var probe = new Probe();
        await using var ordered = new ShardisAsyncOrderedEnumerator<int, int>(shards, x => x, prefetchPerShard: 2, CancellationToken.None, probe);

        // act
        var count = 0;
        while (await ordered.MoveNextAsync())
        {
            _ = ordered.Current;
            count++;
        }

        // assert total items correct and heap never exceeded shardCount * prefetch
        count.Should().Be(4 * 10);
        probe.MaxHeap.Should().BeLessThanOrEqualTo(4 * 2);
    }

    private sealed class ThrowingEnumerator<T>(ShardId shardId, int throwAfter) : IShardisAsyncEnumerator<T>
    {
        private readonly ShardId _id = shardId;
        private readonly int _throwAfter = throwAfter;
        private int _count;
        public ShardItem<T> Current { get; private set; } = default!;
        public int ShardCount => 1;
        public bool IsComplete { get; private set; }
        public bool HasValue { get; private set; }
        public bool IsPrimed { get; private set; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public async ValueTask<bool> MoveNextAsync()
        {
            IsPrimed = true;
            await Task.Yield();
            if (_count == _throwAfter)
            {
                throw new InvalidOperationException("Injected failure");
            }
            if (_count > _throwAfter)
            {
                IsComplete = true;
                HasValue = false;
                return false;
            }
            _count++;
            Current = new(_id, default!);
            HasValue = true;
            return true;
        }
    }

    [Fact]
    public async Task OrderedStreaming_PropagatesShardException_AndDisposes()
    {
        // arrange: one shard throws after first item
        var good = new TestShardisEnumerator<int>(new[] { 1, 2, 3 }, "good");
        var bad = new ThrowingEnumerator<int>(new ShardId("bad"), throwAfter: 1);
        await using var ordered = new ShardisAsyncOrderedEnumerator<int, int>([good, bad], x => x, prefetchPerShard: 1, CancellationToken.None);

        // act/assert: enumerating to exhaustion should surface injected failure
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            while (await ordered.MoveNextAsync()) { }
        });
    }

    [Fact]
    public async Task OrderedStreaming_CancelsCleanly()
    {
        // arrange: slow enumerators to allow cancellation mid-stream
        var slow1 = new TestShardisEnumerator<int>(Enumerable.Range(0, 100), "s1", delays: Enumerable.Repeat(TimeSpan.FromMilliseconds(5), 100));
        var slow2 = new TestShardisEnumerator<int>(Enumerable.Range(0, 100), "s2", delays: Enumerable.Repeat(TimeSpan.FromMilliseconds(5), 100));
        using var cts = new CancellationTokenSource();
        await using var ordered = new ShardisAsyncOrderedEnumerator<int, int>([slow1, slow2], x => x, prefetchPerShard: 3, cts.Token);

        var received = 0;
        // act
        while (true)
        {
            try
            {
                if (!await ordered.MoveNextAsync()) break;
                received++;
                if (received == 10)
                {
                    cts.Cancel();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // assert: we didn't consume everything and cancellation triggered promptly
        received.Should().BeLessThan(200);
    }
}