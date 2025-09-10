namespace Shardis.Query.Diagnostics;

/// <summary>No-op query metrics sink.</summary>
public sealed class NoopShardisQueryMetrics : IShardisQueryMetrics
{
    /// <summary>Singleton.</summary>
    public static readonly IShardisQueryMetrics Instance = new NoopShardisQueryMetrics();
    private NoopShardisQueryMetrics() { }
    /// <inheritdoc />
    public void RecordQueryMergeLatency(double milliseconds, in QueryMetricTags tags) { }
}