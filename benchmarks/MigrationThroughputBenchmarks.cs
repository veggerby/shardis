using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;

using Shardis.Migration.Execution;
using Shardis.Migration.InMemory;
using Shardis.Migration.Instrumentation;
using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Benchmarks;

// Measures end-to-end key migration throughput across a matrix of copy/verify concurrency.
// NOTE: Synthetic in-memory mover + swapper; results are relative, not absolute production figures.
// Benchmark strategy:
//  - Baseline (Copy=1, Verify=1) for relative comparison
//  - Scale keys to expose throughput shifts (1k, 10k, 100k)
//  - Vary concurrency, interleave mode, and swap batch size
//  - Keep retries disabled to minimize variance; resilience measured elsewhere
[MemoryDiagnoser]
[ThreadingDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
[BenchmarkCategory("migration")]
[Config(typeof(Config))]
public class MigrationThroughputBenchmarks
{
    private sealed class Config : ManualConfig
    {
        public Config()
        {
            // Guard against duplicate registrations (BDN may auto-add some exporters).
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // ManualConfig does not expose a direct collection we can query before adding; rely on our own set.
            void AddOnce(IExporter exporter)
            {
                if (existing.Add(exporter.Name))
                {
                    AddExporter(exporter);
                }
            }

            AddOnce(JsonExporter.Full);
            AddOnce(MarkdownExporter.GitHub);

            var baseJob = Job.Default
                .WithRuntime(CoreRuntime.Core80)
                .WithId("migration");

            // Environment variable controls depth of statistical rigor; defaults to quick signal.
            var mode = Environment.GetEnvironmentVariable("SHARDIS_BENCH_MODE");
            if (string.Equals(mode, "full", StringComparison.OrdinalIgnoreCase))
            {
                // Longer run for local deep analysis
                AddJob(baseJob
                    .WithIterationCount(15)
                    .WithWarmupCount(5)
                    .WithLaunchCount(1));
            }
            else
            {
                // Fast signal (roughly equivalent to ShortRunJob)
                AddJob(baseJob
                    .WithIterationCount(3)
                    .WithWarmupCount(3)
                    .WithLaunchCount(1));
            }
        }
    }

    // Matrix control:
    //  - SHARDIS_FULL=1 expands to larger exploratory matrix (local analysis only).
    //  - SHARDIS_CI=1 forces SMALL matrix regardless of SHARDIS_FULL to bound CI runtime.
    private static readonly bool FullMatrixRequested = Environment.GetEnvironmentVariable("SHARDIS_FULL") == "1";
    private static readonly bool CiMode = Environment.GetEnvironmentVariable("SHARDIS_CI") == "1";

    private static readonly int[] KeysSmall = [10_000];
    private static readonly int[] CopySmall = [1, 4];
    private static readonly int[] VerifySmall = [1, 4];
    private static readonly bool[] InterleaveSmall = [true];
    private static readonly int[] SwapSmall = [100];

    private static readonly int[] KeysFull = [1_000, 10_000, 100_000];
    private static readonly int[] CopyFull = [1, 4, 16];
    private static readonly int[] VerifyFull = [1, 4, 16];
    private static readonly bool[] InterleaveFull = [true, false];
    private static readonly int[] SwapFull = [10, 100, 1_000];

    private static bool UseFullMatrix => FullMatrixRequested && !CiMode;

    [ParamsSource(nameof(KeysValues))] public int Keys { get; set; }
    [ParamsSource(nameof(CopyValues))] public int CopyConcurrency { get; set; }
    [ParamsSource(nameof(VerifyValues))] public int VerifyConcurrency { get; set; }
    [ParamsSource(nameof(InterleaveValues))] public bool InterleaveCopyAndVerify { get; set; }
    [ParamsSource(nameof(SwapValues))] public int SwapBatchSize { get; set; }

    public IEnumerable<int> KeysValues => UseFullMatrix ? KeysFull : KeysSmall;
    public IEnumerable<int> CopyValues => UseFullMatrix ? CopyFull : CopySmall;
    public IEnumerable<int> VerifyValues => UseFullMatrix ? VerifyFull : VerifySmall;
    public IEnumerable<bool> InterleaveValues => UseFullMatrix ? InterleaveFull : InterleaveSmall;
    public IEnumerable<int> SwapValues => UseFullMatrix ? SwapFull : SwapSmall;

    private MigrationPlan<string>? _plan;
    private ShardMigrationExecutor<string>? _executor;
    private InMemoryDataMover<string>? _mover;
    private InMemoryMapSwapper<string>? _swapper;
    private FullEqualityVerificationStrategy<string>? _verification;
    private InMemoryCheckpointStore<string>? _checkpoint;

    [GlobalSetup]
    public void Setup()
    {
        // Pre-build immutable migration plan (deterministic key ordering) once per parameter set
        var moves = new List<KeyMove<string>>(Keys);
        for (int i = 0; i < Keys; i++)
        {
            moves.Add(new KeyMove<string>(new ShardKey<string>("k" + i), new("S"), new("T")));
        }
        _plan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, moves);

        _mover = new InMemoryDataMover<string>();
        _verification = new FullEqualityVerificationStrategy<string>(_mover);
        _swapper = new InMemoryMapSwapper<string>(new Shardis.Persistence.InMemoryShardMapStore<string>());
        _checkpoint = new InMemoryCheckpointStore<string>();
        var metrics = new NoOpShardMigrationMetrics(); // eliminate metric overhead
        var options = new ShardMigrationOptions
        {
            CopyConcurrency = CopyConcurrency,
            VerifyConcurrency = VerifyConcurrency,
            InterleaveCopyAndVerify = InterleaveCopyAndVerify,
            SwapBatchSize = SwapBatchSize,
            MaxRetries = 0,
            RetryBaseDelay = TimeSpan.FromMilliseconds(1)
        };
        _executor = new ShardMigrationExecutor<string>(_mover, _verification, _swapper, _checkpoint, metrics, options);

        // Warm a tiny migration (100 keys) to prime JIT & ThreadPool without influencing measured plan
        var warmMoves = new List<KeyMove<string>>(Math.Min(100, Keys));
        for (int i = 0; i < warmMoves.Capacity; i++)
        {
            warmMoves.Add(new KeyMove<string>(new ShardKey<string>("warm" + i), new("S"), new("T")));
        }
        var warmPlan = new MigrationPlan<string>(Guid.NewGuid(), DateTimeOffset.UtcNow, warmMoves);
        // Fire and forget warm-up (sync wait avoided; use GetAwaiter to ensure completion before measuring)
        _executor.ExecuteAsync(warmPlan, progress: null, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true, Description = "Migrate plan (throughput)")]
    [BenchmarkCategory("migration", "throughput")]
    public async Task Migration_Throughput()
    {
        await _executor!.ExecuteAsync(_plan!, progress: null, CancellationToken.None);
    }
}