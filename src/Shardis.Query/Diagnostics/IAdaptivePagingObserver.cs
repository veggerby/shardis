namespace Shardis.Query.Diagnostics;

/// <summary>
/// Observer for adaptive paging decisions. Optional: supply to capture tuning signals (diagnostics/metrics).
/// </summary>
public interface IAdaptivePagingObserver
{
    /// <summary>Called when a new page size decision is made.</summary>
    void OnPageDecision(int shardId, int previousSize, int nextSize, TimeSpan lastBatchLatency);

    /// <summary>Called when rapid oscillation (frequent decisions) is detected within a time window.</summary>
    /// <param name="shardId">Logical shard identifier.</param>
    /// <param name="decisionsInWindow">Number of decisions observed inside the oscillation window.</param>
    /// <param name="window">Window size used for detection.</param>
    void OnOscillationDetected(int shardId, int decisionsInWindow, TimeSpan window);

    /// <summary>Called once after enumeration completes with the final decided page size and total decision count.</summary>
    /// <param name="shardId">Logical shard identifier.</param>
    /// <param name="finalSize">Final page size used.</param>
    /// <param name="totalDecisions">Total number of size change decisions made.</param>
    void OnFinalPageSize(int shardId, int finalSize, int totalDecisions);
}

/// <summary>No-op implementation.</summary>
public sealed class NoopAdaptivePagingObserver : IAdaptivePagingObserver
{
    /// <summary>Singleton instance.</summary>
    public static readonly IAdaptivePagingObserver Instance = new NoopAdaptivePagingObserver();
    private NoopAdaptivePagingObserver() { }
    /// <inheritdoc />
    public void OnPageDecision(int shardId, int previousSize, int nextSize, TimeSpan lastBatchLatency) { }
    /// <inheritdoc />
    public void OnOscillationDetected(int shardId, int decisionsInWindow, TimeSpan window) { }
    /// <inheritdoc />
    public void OnFinalPageSize(int shardId, int finalSize, int totalDecisions) { }
}