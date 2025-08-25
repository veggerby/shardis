using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using Shardis.Hashing;

namespace Shardis.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class HasherBenchmarks
{
    private readonly IShardRingHasher _default = DefaultShardRingHasher.Instance;
    private readonly IShardRingHasher _fnv = Fnv1aShardRingHasher.Instance;
    private readonly uint[] _values;

    public HasherBenchmarks()
    {
        var rnd = new Random(42);
        _values = Enumerable.Range(0, 50_000).Select(_ => (uint)rnd.Next(int.MinValue, int.MaxValue)).ToArray();
    }

    [Benchmark]
    public ulong DefaultHasher()
    {
        ulong acc = 0;
        foreach (var v in _values)
        {
            acc ^= _default.Hash(v.ToString());
        }
        return acc;
    }

    [Benchmark]
    public ulong Fnv1aHasher()
    {
        ulong acc = 0;
        foreach (var v in _values)
        {
            acc ^= _fnv.Hash(v.ToString());
        }
        return acc;
    }
}

public static class HashProgram
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<HasherBenchmarks>();
    }
}