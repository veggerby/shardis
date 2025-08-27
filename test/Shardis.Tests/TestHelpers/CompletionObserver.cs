using System.Collections.Concurrent;

using Shardis.Model;
using Shardis.Querying;

namespace Shardis.Tests;

public sealed class CompletionObserver : IMergeObserver
{
    public ConcurrentBag<ShardId> Completed { get; } = new();
    public ConcurrentBag<(ShardId, ShardStopReason)> Stopped { get; } = new();
    public void OnShardCompleted(ShardId s) => Completed.Add(s);
    public void OnShardStopped(ShardId s, ShardStopReason r) => Stopped.Add((s, r));
    public void OnItemYielded(ShardId _) { }
    public void OnBackpressureWaitStart() { }
    public void OnBackpressureWaitStop() { }
    public void OnHeapSizeSample(int _) { }
}