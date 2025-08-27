using System.Collections.Concurrent;
using System.Diagnostics;

using Shardis.Model;
using Shardis.Querying;

namespace Shardis.Tests;

public sealed class YieldRecordingObserver : IMergeObserver
{
    private long _seq;
    public ConcurrentQueue<(long seq, long tUs, ShardId shard)> Events { get; } = new();
    public void OnItemYielded(ShardId shard) => Events.Enqueue((Interlocked.Increment(ref _seq), TimestampUs(), shard));
    public void OnShardCompleted(ShardId shard) { }
    public void OnShardStopped(ShardId shard, ShardStopReason reason) { }
    public void OnBackpressureWaitStart() { }
    public void OnBackpressureWaitStop() { }
    public void OnHeapSizeSample(int _) { }
    private static long TimestampUs() => (long)(Stopwatch.GetTimestamp() * (1_000_000.0 / Stopwatch.Frequency));
}