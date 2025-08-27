using Shardis.Model;
using Shardis.Querying;

namespace Shardis.Tests;

public sealed class CompositeObserver(IMergeObserver[] observers) : IMergeObserver
{
    private readonly IMergeObserver[] _observers = observers;
    public void OnItemYielded(ShardId shardId) { foreach (var o in _observers) o.OnItemYielded(shardId); }
    public void OnShardCompleted(ShardId shardId) { foreach (var o in _observers) o.OnShardCompleted(shardId); }
    public void OnShardStopped(ShardId shardId, ShardStopReason reason) { foreach (var o in _observers) o.OnShardStopped(shardId, reason); }
    public void OnBackpressureWaitStart() { foreach (var o in _observers) o.OnBackpressureWaitStart(); }
    public void OnBackpressureWaitStop() { foreach (var o in _observers) o.OnBackpressureWaitStop(); }
    public void OnHeapSizeSample(int size) { foreach (var o in _observers) o.OnHeapSizeSample(size); }
}