namespace Shardis.Query.Diagnostics;

/// <summary>Observer hooks for query execution lifecycle (optional).</summary>
public interface IQueryMetricsObserver
{
    /// <summary>Signals shard enumeration starting.</summary>
    void OnShardStart(int shardId);
    /// <summary>Signals that <paramref name="count"/> items were produced for a shard (batch granularity may be 1).</summary>
    void OnItemsProduced(int shardId, int count);
    /// <summary>Signals shard enumeration completed (normal or exhausted).</summary>
    void OnShardStop(int shardId);
    /// <summary>Signals the overall merged enumeration completed.</summary>
    void OnCompleted();
    /// <summary>Signals cancellation observed and enumeration ceased early.</summary>
    void OnCanceled();
}