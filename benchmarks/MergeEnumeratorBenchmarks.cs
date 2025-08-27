using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;

using Shardis.Model;
using Shardis.Querying;
using Shardis.Testing;

namespace Shardis.Benchmarks;

/// <summary>
/// Benchmarks comparing unordered streaming vs ordered streaming vs ordered eager merge enumerators.
/// Captures throughput (items/sec) via BenchmarkDotNet and first-item latency distributions (p50/p95) exported as CSV.
/// Deterministic per-shard delay schedules enable reproducible skew scenarios.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[MarkdownExporter]
[CsvExporter]
[BenchmarkCategory("merge")]
[Config(typeof(Config))]
public class MergeEnumeratorBenchmarks
{
    // Parameters -------------------------------------------------------------
    [Params(2, 4, 8)] public int ShardCount { get; set; }
    [Params(1_000, 10_000)] public int ItemsPerShard { get; set; }
    [Params("none", "1:3", "1:10")] public string SkewProfile { get; set; } = "none"; // maps to Determinism.Skew
    [Params(0, 64, 256)] public int Capacity { get; set; } // applies ONLY to unordered path (ordered modes ignore, kept for grouping)
    [Params(1, 2, 4)] public int PrefetchPerShard { get; set; }
    [Params(1337)] public int Seed { get; set; }

    // State ------------------------------------------------------------------
    private Determinism _det = null!;
    private TimeSpan[][] _delaySchedules = null!;
    private TestShard[] _shards = null!;
    private ShardStreamBroadcaster<IShard<int>, int> _unordered = null!;
    private ShardStreamBroadcaster<IShard<int>, int> _ordered = null!; // shared for ordered streaming & eager

    private readonly Stopwatch _sw = new();

    // Static accumulators (BenchmarkDotNet creates new instances per param combination) -----
    private static readonly object _lock = new();
    private static readonly List<Record> _records = [];

    private record struct Record(int Seed, int Shards, int ItemsPerShard, string Skew, int Capacity, int Prefetch, string Method, long FirstItemUs);

    // Setup ------------------------------------------------------------------
    [GlobalSetup]
    public void GlobalSetup()
    {
        _det = Determinism.Create(Seed);
        var skew = SkewProfile switch
        {
            "none" => Skew.None,
            "1:3" => Skew.Mild,
            "1:10" => Skew.Harsh,
            _ => Skew.None
        };
        _delaySchedules = _det.MakeDelays(ShardCount, skew, TimeSpan.FromMilliseconds(1), steps: ItemsPerShard);

        _shards = new TestShard[ShardCount];
        for (int i = 0; i < ShardCount; i++)
        {
            _shards[i] = new TestShard(i, $"shard-{i}", ItemsPerShard, _delaySchedules, _det);
        }

        _unordered = Capacity == 0
            ? new ShardStreamBroadcaster<IShard<int>, int>(_shards)
            : new ShardStreamBroadcaster<IShard<int>, int>(_shards, Capacity);
        _ordered = new ShardStreamBroadcaster<IShard<int>, int>(_shards);
    }

    // Benchmarks --------------------------------------------------------------

    [Benchmark(Baseline = true, Description = "Unordered_Streaming_TotalItems")]
    public async Task<int> Unordered_Streaming()
    {
        int count = 0;
        long firstItem = -1;
        _sw.Restart();
        await foreach (var item in _unordered.QueryAllShardsAsync(s => _shards[s].Stream()))
        {
            if (firstItem < 0) { firstItem = ElapsedMicros(_sw); }
            count++;
        }
        RecordLatency("Unordered_Streaming", firstItem);
        return count;
    }

    [Benchmark(Description = "Ordered_Streaming_TotalItems")]
    public async Task<int> Ordered_Streaming()
    {
        int count = 0;
        long firstItem = -1;
        _sw.Restart();
        await foreach (var item in _ordered.QueryAllShardsOrderedStreamingAsync(s => _shards[s].Stream(), x => x, prefetchPerShard: PrefetchPerShard))
        {
            if (firstItem < 0) { firstItem = ElapsedMicros(_sw); }
            count++;
        }
        RecordLatency("Ordered_Streaming", firstItem);
        return count;
    }

    [Benchmark(Description = "Ordered_Eager_TotalItems")]
    public async Task<int> Ordered_Eager()
    {
        int count = 0;
        long firstItem = -1;
        _sw.Restart();
        await foreach (var item in _ordered.QueryAllShardsOrderedEagerAsync(s => _shards[s].Stream(), x => x))
        {
            if (firstItem < 0) { firstItem = ElapsedMicros(_sw); }
            count++;
        }
        RecordLatency("Ordered_Eager", firstItem);
        return count;
    }

    private void RecordLatency(string method, long firstItemUs)
    {
        lock (_lock)
        {
            _records.Add(new(Seed, ShardCount, ItemsPerShard, SkewProfile, Capacity, PrefetchPerShard, method, firstItemUs));
        }
    }

    // Cleanup ----------------------------------------------------------------
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        // Aggregate percentiles by (Seed,Shards,ItemsPerShard,Skew,Capacity,Prefetch,Method)
        List<Record> snapshot;
        lock (_lock) { snapshot = _records.ToList(); }
        var groups = snapshot
            .GroupBy(r => new { r.Seed, r.Shards, r.ItemsPerShard, r.Skew, r.Capacity, r.Prefetch, r.Method })
            .OrderBy(g => g.Key.Shards).ThenBy(g => g.Key.ItemsPerShard).ThenBy(g => g.Key.Method);

        var sb = new StringBuilder();
        sb.AppendLine("Seed,Shards,ItemsPerShard,Skew,Capacity,PrefetchPerShard,Method,Samples,P50FirstItemUs,P95FirstItemUs");
        foreach (var g in groups)
        {
            var arr = g.Select(r => r.FirstItemUs).OrderBy(v => v).ToArray();
            if (arr.Length == 0) { continue; }
            double p50 = Percentile(arr, 0.50);
            double p95 = Percentile(arr, 0.95);
            sb.AppendLine($"{g.Key.Seed},{g.Key.Shards},{g.Key.ItemsPerShard},{g.Key.Skew},{g.Key.Capacity},{g.Key.Prefetch},{g.Key.Method},{arr.Length},{p50:F0},{p95:F0}");
        }

        try
        {
            Directory.CreateDirectory("BenchmarkDotNet.Artifacts/results");
            // Single aggregated file across all methods. Name includes 'all-methods' to avoid confusion if user filters methods.
            var file = $"BenchmarkDotNet.Artifacts/results/merge-first-item-latency-all-methods-seed{Seed}.csv";
            File.WriteAllText(file, sb.ToString());
            Console.WriteLine("[merge] first-item latency percentiles written => " + file);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[merge] failed to write latency CSV: " + ex.Message);
        }
    }

    // Helpers ----------------------------------------------------------------
    private static long ElapsedMicros(Stopwatch sw) => (long)(sw.ElapsedTicks * (1_000_000.0 / Stopwatch.Frequency));
    private static double Percentile(long[] sortedAscending, double p)
    {
        if (sortedAscending.Length == 0) return double.NaN;
        var idx = (int)Math.Ceiling(p * sortedAscending.Length) - 1;
        if (idx < 0) idx = 0;
        if (idx >= sortedAscending.Length) idx = sortedAscending.Length - 1;
        return sortedAscending[idx];
    }

    private sealed class TestShard(int index, string id, int count, TimeSpan[][] schedules, Determinism det) : IShard<int>
    {
        public ShardId ShardId { get; } = new(id);
        public int CreateSession() => index;
        public Querying.Linq.IShardQueryExecutor<int> QueryExecutor => DummyExecutor.Instance;

        // Enumerates strictly increasing integers with deterministic per-item delay.
        // The [EnumeratorCancellation] attribute enables cooperative cancellation if benchmarks add cancellation later.
        public async IAsyncEnumerable<int> Stream([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < count; i++)
            {
                await det.DelayForShardAsync(schedules, index, i, cancellationToken).ConfigureAwait(false);
                yield return i; // strictly increasing ordering inside shard
            }
        }

        private sealed class DummyExecutor : Querying.Linq.IShardQueryExecutor<int>
        {
            public static readonly DummyExecutor Instance = new();
            public IAsyncEnumerable<T> Execute<T>(int session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IQueryable<T>>> linqExpr) where T : notnull => throw new NotSupportedException();
            public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(int session, System.Linq.Expressions.Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr, Func<T, TKey> keySelector) where T : notnull => throw new NotSupportedException();
        }
    }

    private sealed class Config : ManualConfig
    {
        public Config()
        {
            // Ensure GitHub-flavored markdown exporter is present (attribute only adds default markdown)
            AddExporter(MarkdownExporter.GitHub);
        }
    }
}