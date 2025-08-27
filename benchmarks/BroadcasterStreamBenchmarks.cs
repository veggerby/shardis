using BenchmarkDotNet.Attributes;

using Shardis.Model;
using Shardis.Querying;
using Shardis.Querying.Linq;
using Shardis.Testing;

namespace Shardis.Benchmarks;

[MemoryDiagnoser]
public class BroadcasterStreamBenchmarks
{
    private sealed class BenchShard(string id, string session, IEnumerable<TimeSpan> delays) : IShard<string>
    {
        public ShardId ShardId { get; } = new(id);
        private readonly List<TimeSpan> _delays = delays.ToList();
        public string CreateSession() => session;
        public IShardQueryExecutor<string> QueryExecutor => throw new NotSupportedException();
        public async IAsyncEnumerable<int> Produce()
        {
            for (int i = 0; i < _delays.Count; i++)
            {
                if (_delays[i] > TimeSpan.Zero)
                {
                    await Task.Delay(_delays[i]);
                }
                yield return i;
            }
        }
    }

    private ShardStreamBroadcaster<IShard<string>, string> _broadcaster = null!;
    private Dictionary<string, BenchShard> _bySession = null!; // session -> shard
    private Determinism _det = null!;

    [Params(1337)]
    public int Seed { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _det = Determinism.Create(Seed);

        // deterministic delay schedules (fast vs slow profile)
        var fast = new BenchShard("fast", "fs", Enumerable.Repeat(TimeSpan.FromMilliseconds(1), 50));
        var slow = new BenchShard("slow", "ss", Enumerable.Repeat(TimeSpan.FromMilliseconds(5), 50));
        _broadcaster = new([fast, slow]);
        _bySession = new()
        {
            [fast.CreateSession()] = fast,
            [slow.CreateSession()] = slow
        };
    }

    [Benchmark]
    public async Task<int> StreamFastVsSlow()
    {
        int count = 0;
        await foreach (var item in _broadcaster.QueryAllShardsAsync(s => _bySession[s].Produce()))
        {
            count++;
        }
        return count;
    }
}