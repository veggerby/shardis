using BenchmarkDotNet.Running;

using Shardis.Benchmarks;

// BenchmarkDotNet idiomatic entrypoint using BenchmarkSwitcher for discovery & filtering.
// Supports native args: --filter, --anyCategories, --allCategories, --list, --runtimes, etc.
// Optional default: when no args provided, restrict to migration category for fast local signal.
var switcher = new BenchmarkSwitcher(
[
    typeof(AdaptivePagingAllocationsBenchmarks),
    typeof(AdaptivePagingBenchmarks),
    typeof(BroadcasterStreamBenchmarks),
    typeof(HasherBenchmarks),
    typeof(MergeEnumeratorBenchmarks),
    typeof(MigrationThroughputBenchmarks),
    typeof(PipelineCacheBenchmarks),
    typeof(QueryBenchmarks),
    typeof(QueryLatencyEmissionBenchmarks),
    typeof(RouterBenchmarks),
    typeof(SegmentedPlannerBenchmarks),
]);

// if (args.Length == 0)
// {
//     // Default to migration benchmarks only (can override by passing args)
//     args = ["--anyCategories", "migration"];
// }

var summaries = switcher.Run(args);
// Treat any benchmark with non-empty validation errors as failure.
var failed = summaries.Any(s => s.HasCriticalValidationErrors);
return failed ? 1 : 0;