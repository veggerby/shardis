namespace Shardis.Querying;

using System.Runtime.CompilerServices;

using Shardis.Model;

/// <summary>
/// Observer hook for merge / broadcast operations providing low-overhead instrumentation callbacks.
/// Implementations must be thread-safe and return quickly; callbacks MAY be invoked concurrently from multiple shard worker threads.
/// </summary>
public interface IMergeObserver
{
    /// <summary>Called after an item has been yielded to a downstream consumer.</summary>
    void OnItemYielded(ShardId shardId);

    /// <summary>Called once for each shard when its upstream asynchronous stream completes.</summary>
    void OnShardCompleted(ShardId shardId);

    /// <summary>
    /// Called exactly once per shard when the shard stops producing items for any reason.
    /// Implementations can distinguish lifecycle end reasons without relying on exception side-channels.
    /// </summary>
    void OnShardStopped(ShardId shardId, ShardStopReason reason);

    /// <summary>Called when a producer encounters backpressure and must await capacity (unordered/channel path).</summary>
    void OnBackpressureWaitStart();

    /// <summary>Called after a backpressure wait completes and the producer resumes writing.</summary>
    void OnBackpressureWaitStop();

    /// <summary>Called when the ordered merge samples current heap size (ordered path). Sampling frequency is implementation defined.</summary>
    void OnHeapSizeSample(int size);
}

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

/// <summary>
/// Reason a shard stream stopped.
/// </summary>
public enum ShardStopReason
{
    /// <summary>Shard enumerated all items successfully.</summary>
    Completed = 0,
    /// <summary>Enumeration canceled due to external cancellation token.</summary>
    Canceled = 1,
    /// <summary>Enumeration terminated because of an unhandled exception.</summary>
    Faulted = 2
}