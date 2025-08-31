namespace Shardis.Query.Diagnostics;

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