namespace Shardis.Query.Diagnostics;

/// <summary>No-op implementation of <see cref="IQueryMetricsObserver"/> (default when metrics not supplied).</summary>
public sealed class NoopQueryMetricsObserver : IQueryMetricsObserver
{
    /// <summary>Singleton instance.</summary>
    public static readonly IQueryMetricsObserver Instance = new NoopQueryMetricsObserver();
    private NoopQueryMetricsObserver() { }
    /// <inheritdoc />
    public void OnShardStart(int shardId) { }
    /// <inheritdoc />
    public void OnItemsProduced(int shardId, int count) { }
    /// <inheritdoc />
    public void OnShardStop(int shardId) { }
    /// <inheritdoc />
    public void OnCompleted() { }
    /// <inheritdoc />
    public void OnCanceled() { }
}