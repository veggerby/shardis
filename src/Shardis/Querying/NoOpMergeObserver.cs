namespace Shardis.Querying;

using System.Runtime.CompilerServices;

using Shardis.Model;

/// <summary>
/// No-op merge observer (default) – all callbacks are empty.
/// </summary>
internal sealed class NoOpMergeObserver : IMergeObserver
{
    public static readonly IMergeObserver Instance = new NoOpMergeObserver();
    private NoOpMergeObserver() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void OnItemYielded(ShardId shardId) { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void OnShardCompleted(ShardId shardId) { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void OnShardStopped(ShardId shardId, ShardStopReason reason) { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void OnBackpressureWaitStart() { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void OnBackpressureWaitStop() { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void OnHeapSizeSample(int size) { }
}