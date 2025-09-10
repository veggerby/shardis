using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

using Shardis.Migration.InMemory;
using Shardis.Migration.Model;
using Shardis.Migration.Planning;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SegmentedPlannerBenchmarks
{
    [Params(10_000, 100_000)] public int KeyCount;
    [Params(5_000)] public int Moves; // number of keys that will change shard
    [Params(10_000)] public int SegmentSize;

    private TopologySnapshot<string> _target = default!;
    private TopologySnapshot<string> _source = default!;
    private InMemoryShardMapStore<string> _sourceStore = default!;
    private SegmentedEnumerationMigrationPlanner<string> _segmented = default!;
    private InMemoryMigrationPlanner<string> _inMemory = default!;

    [GlobalSetup]
    public void Setup()
    {
        _sourceStore = new InMemoryShardMapStore<string>();
        var targetAssignments = new Dictionary<ShardKey<string>, ShardId>(KeyCount);
        for (int i = 0; i < KeyCount; i++)
        {
            var key = new ShardKey<string>($"k{i:000000}");
            var fromShard = new ShardId("0");
            var toShard = (i < Moves) ? new ShardId("1") : fromShard;
            _sourceStore.AssignShardToKey(key, fromShard);
            targetAssignments[key] = toShard;
        }
        // Build source snapshot from enumeration (small cost for benchmark setup)
        var srcDict = new Dictionary<ShardKey<string>, ShardId>(KeyCount);
        var enumTask = BuildSourceAsync();
        enumTask.GetAwaiter().GetResult();
        async Task BuildSourceAsync()
        {
            await foreach (var map in _sourceStore.EnumerateAsync())
            {
                srcDict[map.ShardKey] = map.ShardId;
            }
        }
        _source = new TopologySnapshot<string>(srcDict);
        _target = new TopologySnapshot<string>(targetAssignments);
        _segmented = new SegmentedEnumerationMigrationPlanner<string>(_sourceStore, SegmentSize);
        _inMemory = new InMemoryMigrationPlanner<string>();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Plan")] public Task<MigrationPlan<string>> InMemoryPlanner() => _inMemory.CreatePlanAsync(_source, _target, CancellationToken.None);

    [Benchmark, BenchmarkCategory("Plan")] public Task<MigrationPlan<string>> SegmentedPlanner() => _segmented.CreatePlanAsync(_source, _target, CancellationToken.None);
}

public static class SegmentedPlannerBenchmarkProgram
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<SegmentedPlannerBenchmarks>();
    }
}