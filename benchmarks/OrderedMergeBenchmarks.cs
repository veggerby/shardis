using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;

namespace Shardis.Benchmarks;

/// <summary>
/// Compares buffered ordered merge (materialize then sort) vs streaming k-way merge for varying shard/size configurations.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[MarkdownExporter]
[Config(typeof(Cfg))]
public class OrderedMergeBenchmarks
{
    [Params(2, 4, 8)] public int Shards { get; set; }
    [Params(100, 10_000)] public int ItemsPerShard { get; set; }
    [Params(1337)] public int Seed { get; set; }

    private IAsyncEnumerable<int>[] _ascending = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rnd = new Random(Seed);
        _ascending = new IAsyncEnumerable<int>[Shards];
        for (int s = 0; s < Shards; s++)
        {
            // strictly increasing per shard; offset to interleave
            var start = s * ItemsPerShard;
            var data = Enumerable.Range(start, ItemsPerShard).Select(x => x).ToArray();
            _ascending[s] = Enumerate(data);
        }
    }

    private static async IAsyncEnumerable<int> Enumerate(int[] values)
    {
        foreach (var v in values)
        {
            await Task.Yield();
            yield return v;
        }
    }

    [Benchmark(Baseline = true)]
    public async Task<int> Streaming()
    {
        int count = 0;
        await foreach (var v in Query.OrderedMergeHelper.MergeStreaming(_ascending, x => x))
        {
            count++;
        }
        return count;
    }

    [Benchmark]
    public async Task<int> Buffered()
    {
        // emulate buffered: materialize all then OrderBy
        var all = new List<int>();
        foreach (var src in _ascending)
        {
            await foreach (var v in src)
            {
                all.Add(v);
            }
        }
        int count = 0;
        foreach (var v in all.OrderBy(x => x))
        {
            count++;
        }
        return count;
    }

    private sealed class Cfg : ManualConfig
    {
        public Cfg()
        {
            AddExporter(MarkdownExporter.GitHub);
        }
    }
}