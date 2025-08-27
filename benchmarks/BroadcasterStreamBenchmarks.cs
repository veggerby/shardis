using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;

using Shardis.Model;
using Shardis.Querying;
using Shardis.Querying.Linq;
using Shardis.Testing;

namespace Shardis.Benchmarks;

/// <summary>
/// Capacity sweep for unordered broadcaster path measuring backpressure wait behavior & first-item latency.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[MarkdownExporter]
[CsvExporter]
[BenchmarkCategory("broadcaster")]
public class BroadcasterStreamBenchmarks
{
    // Parameters (Step 9)
    [Params(2, 4, 8)] public int Shards { get; set; }
    [Params(1_000, 10_000)] public int ItemsPerShard { get; set; }
    [Params("none", "1:3", "1:10")] public string Skew { get; set; } = "none";
    [Params(0, 32, 64, 128, 256, 512)] public int Capacity { get; set; } // 0 = unbounded
    [Params(1337)] public int Seed { get; set; }

    private Determinism _det = null!;
    private TimeSpan[][] _schedules = null!;
    private BenchShard[] _shards = null!;
    private TimeSpan[][] _consumerSchedule = null!; // for consumer-slow variant

    // Metrics accumulation
    private static readonly object _gate = new();
    private static readonly List<RunRecord> _records = [];
    private static bool _recordsCleared;
    private readonly Stopwatch _sw = new();

    private record struct RunRecord(string Scenario, int Seed, int Shards, int ItemsPerShard, string Skew, int Capacity, long FirstItemUs, long WaitCount, long BlockedUs);

    [GlobalSetup]
    public void Setup()
    {
        // one-time static accumulator clear (keep across parameter combinations within a single process execution)
        if (!_recordsCleared)
        {
            lock (_gate)
            {
                _records.Clear();
                _recordsCleared = true;
            }
        }

        _det = Determinism.Create(Seed);
        var skewEnum = Skew switch
        {
            "none" => Testing.Skew.None,
            "1:3" => Testing.Skew.Mild,
            "1:10" => Testing.Skew.Harsh,
            _ => Testing.Skew.None
        };
        _schedules = _det.MakeDelays(Shards, skewEnum, TimeSpan.FromMilliseconds(1), steps: ItemsPerShard);
        _shards = new BenchShard[Shards];
        for (int i = 0; i < Shards; i++)
        {
            _shards[i] = new BenchShard(i, ItemsPerShard, _schedules, _det);
        }

        // consumer schedule (single logical consumer shard index 0) for slow-consumer benchmark
        var totalItems = Shards * ItemsPerShard;
        _consumerSchedule = _det.MakeDelays(1, Testing.Skew.None, TimeSpan.FromMilliseconds(0.15), steps: totalItems);
    }

    [Benchmark(Baseline = true, Description = "Unordered_Streaming_CapacitySweep")]
    public async Task<int> Unordered_Streaming_CapacitySweep()
    {
        var observer = new RecordingObserver();
        var broadcaster = Capacity == 0
            ? new ShardStreamBroadcaster<IShard<int>, int>(_shards, observer: observer)
            : new ShardStreamBroadcaster<IShard<int>, int>(_shards, channelCapacity: Capacity, observer: observer);

        int count = 0;
        long first = -1;
        _sw.Restart();
        await foreach (var item in broadcaster.QueryAllShardsAsync(s => _shards[s].Produce()))
        {
            if (first < 0) { first = ElapsedUs(_sw); }
            count++;
        }
        lock (_gate)
        {
            _records.Add(new("baseline", Seed, Shards, ItemsPerShard, Skew, Capacity, first, observer.BackpressureWaitCount, observer.TotalBlockedUs));
        }
        return count;
    }

    [Benchmark(Description = "Unordered_Streaming_CapacitySweep_ConsumerSlow")]
    public async Task<int> Unordered_Streaming_CapacitySweep_ConsumerSlow()
    {
        var observer = new RecordingObserver();
        var broadcaster = Capacity == 0
            ? new ShardStreamBroadcaster<IShard<int>, int>(_shards, observer: observer)
            : new ShardStreamBroadcaster<IShard<int>, int>(_shards, channelCapacity: Capacity, observer: observer);

        int count = 0;
        long first = -1;
        _sw.Restart();
        await foreach (var item in broadcaster.QueryAllShardsAsync(s => _shards[s].Produce()))
        {
            if (first < 0) { first = ElapsedUs(_sw); }
            count++;
            // deterministic tiny consumer delay (slows reader side)
            await _det.DelayForShardAsync(_consumerSchedule, 0, count - 1).ConfigureAwait(false);
        }
        lock (_gate)
        {
            _records.Add(new("consumer-slow", Seed, Shards, ItemsPerShard, Skew, Capacity, first, observer.BackpressureWaitCount, observer.TotalBlockedUs));
        }
        return count;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        List<RunRecord> snapshot;
        lock (_gate) { snapshot = _records.ToList(); }
        var groups = snapshot
            .GroupBy(r => new { r.Scenario, r.Seed, r.Shards, r.ItemsPerShard, r.Skew, r.Capacity })
            .OrderBy(g => g.Key.Scenario).ThenBy(g => g.Key.Shards).ThenBy(g => g.Key.ItemsPerShard).ThenBy(g => g.Key.Capacity);

        var sb = new StringBuilder();
        sb.AppendLine("Scenario,Seed,Shards,ItemsPerShard,Skew,Capacity,Samples,MedianFirstItemUs,P95FirstItemUs,MedianWaits,MedianBlockedUs,P50BlockedUs,P95BlockedUs");
        foreach (var g in groups)
        {
            var firsts = g.Select(r => r.FirstItemUs).OrderBy(v => v).ToArray();
            var waits = g.Select(r => r.WaitCount).OrderBy(v => v).ToArray();
            var blocks = g.Select(r => r.BlockedUs).OrderBy(v => v).ToArray();
            if (firsts.Length == 0) { continue; }
            double p50First = Percentile(firsts, 0.50);
            double p95First = Percentile(firsts, 0.95);
            long medianWaits = waits[waits.Length / 2];
            long medianBlocked = blocks[blocks.Length / 2];
            double p50Blocked = Percentile(blocks, 0.50);
            double p95Blocked = Percentile(blocks, 0.95);
            sb.AppendLine($"{g.Key.Scenario},{g.Key.Seed},{g.Key.Shards},{g.Key.ItemsPerShard},{g.Key.Skew},{g.Key.Capacity},{firsts.Length},{p50First:F0},{p95First:F0},{medianWaits},{medianBlocked},{p50Blocked:F0},{p95Blocked:F0}");
        }
        try
        {
            Directory.CreateDirectory("BenchmarkDotNet.Artifacts/results");
            var file = $"BenchmarkDotNet.Artifacts/results/broadcaster-capacity-sweep-seed{Seed}.csv";
            File.WriteAllText(file, sb.ToString());
            Console.WriteLine("[broadcaster] capacity sweep percentiles written => " + file);
            Console.WriteLine("Guidance: small capacity increases wait count; moderate (128-256) balances waits & memory; unbounded removes waits but may increase allocation.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[broadcaster] failed to write capacity CSV: " + ex.Message);
        }
    }

    // Helpers --------------------------------------------------------------
    private static long ElapsedUs(Stopwatch sw) => (long)(sw.ElapsedTicks * (1_000_000.0 / Stopwatch.Frequency));
    private static double Percentile(long[] asc, double p)
    {
        if (asc.Length == 0) return double.NaN;
        var idx = (int)Math.Ceiling(p * asc.Length) - 1;
        if (idx < 0) idx = 0; if (idx >= asc.Length) idx = asc.Length - 1; return asc[idx];
    }

    private sealed class BenchShard(int index, int count, TimeSpan[][] schedules, Determinism det) : IShard<int>
    {
        public ShardId ShardId { get; } = new($"shard-{index}");
        public int CreateSession() => index;
        public IShardQueryExecutor<int> QueryExecutor => DummyExecutor.Instance;
        public async IAsyncEnumerable<int> Produce([EnumeratorCancellation] CancellationToken ct = default)
        {
            for (int i = 0; i < count; i++)
            {
                if (ct.IsCancellationRequested) yield break;
                await det.DelayForShardAsync(schedules, index, i, ct).ConfigureAwait(false);
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

    private sealed class RecordingObserver : IMergeObserver
    {
        private long _waits;
        private long _blockedUs;
        private readonly ConcurrentDictionary<int, long> _starts = new();
        public long BackpressureWaitCount => Interlocked.Read(ref _waits);
        public long TotalBlockedUs => Interlocked.Read(ref _blockedUs);
        public void OnBackpressureWaitStart()
        {
            var now = TimestampUs();
            _starts[Environment.CurrentManagedThreadId] = now;
            Interlocked.Increment(ref _waits);
        }
        public void OnBackpressureWaitStop()
        {
            var tid = Environment.CurrentManagedThreadId;
            if (_starts.TryRemove(tid, out var start))
            {
                var delta = TimestampUs() - start;
                if (delta > 0) Interlocked.Add(ref _blockedUs, delta);
            }
        }
        public void OnItemYielded(ShardId shardId) { }
        public void OnShardCompleted(ShardId shardId) { }
        public void OnShardStopped(ShardId shardId, ShardStopReason reason) { }
        public void OnHeapSizeSample(int size) { }
        private static long TimestampUs() => (long)(Stopwatch.GetTimestamp() * (1_000_000.0 / Stopwatch.Frequency));
    }
}