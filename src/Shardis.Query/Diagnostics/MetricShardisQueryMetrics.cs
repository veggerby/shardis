using System.Diagnostics.Metrics;

namespace Shardis.Query.Diagnostics;

/// <summary>Default implementation of <see cref="IShardisQueryMetrics"/> emitting OpenTelemetry metrics.</summary>
public sealed class MetricShardisQueryMetrics : IShardisQueryMetrics
{
    private static readonly Meter Meter = new(Shardis.Diagnostics.ShardisDiagnostics.MeterName, "1.0.0");
    private static readonly Histogram<double> MergeLatency = Meter.CreateHistogram<double>("shardis.query.merge.latency", unit: "ms", description: "End-to-end duration of merged shard query enumeration");
    private static readonly Histogram<double> HealthProbeLatency = Meter.CreateHistogram<double>("shardis.health.probe.latency", unit: "ms", description: "Shard health probe latency");
    private static readonly Counter<int> UnhealthyShardCounter = Meter.CreateCounter<int>("shardis.health.unhealthy.count", description: "Number of unhealthy shards");
    private static readonly Counter<int> ShardSkippedCounter = Meter.CreateCounter<int>("shardis.health.shard.skipped", description: "Number of shards skipped due to health");
    private static readonly Counter<int> ShardRecoveredCounter = Meter.CreateCounter<int>("shardis.health.shard.recovered", description: "Number of shards recovered");

    /// <inheritdoc />
    public void RecordQueryMergeLatency(double milliseconds, in QueryMetricTags tags)
    {
        MergeLatency.Record(milliseconds,
            new KeyValuePair<string, object?>("db.system", tags.DbSystem ?? string.Empty),
            new KeyValuePair<string, object?>("provider", tags.Provider ?? string.Empty),
            new KeyValuePair<string, object?>("shard.count", tags.ShardCount),
            new KeyValuePair<string, object?>("target.shard.count", tags.TargetShardCount),
            new KeyValuePair<string, object?>("invalid.shard.count", tags.InvalidShardCount),
            new KeyValuePair<string, object?>("merge.strategy", tags.MergeStrategy ?? string.Empty),
            new KeyValuePair<string, object?>("ordering.buffered", tags.OrderingBuffered ?? string.Empty),
            new KeyValuePair<string, object?>("fanout.concurrency", tags.FanoutConcurrency),
            new KeyValuePair<string, object?>("channel.capacity", tags.ChannelCapacity),
            new KeyValuePair<string, object?>("failure.mode", tags.FailureMode ?? string.Empty),
            new KeyValuePair<string, object?>("result.status", tags.ResultStatus ?? string.Empty),
            new KeyValuePair<string, object?>("root.type", tags.RootType ?? string.Empty));
    }

    /// <inheritdoc />
    public void RecordHealthProbeLatency(double milliseconds, string shardId, string status)
    {
        HealthProbeLatency.Record(milliseconds,
            new KeyValuePair<string, object?>("shard.id", shardId),
            new KeyValuePair<string, object?>("health.status", status));
    }

    /// <inheritdoc />
    public void RecordUnhealthyShardCount(int count)
    {
        UnhealthyShardCounter.Add(count);
    }

    /// <inheritdoc />
    public void RecordShardSkipped(string shardId, string reason)
    {
        ShardSkippedCounter.Add(1,
            new KeyValuePair<string, object?>("shard.id", shardId),
            new KeyValuePair<string, object?>("reason", reason));
    }

    /// <inheritdoc />
    public void RecordShardRecovered(string shardId)
    {
        ShardRecoveredCounter.Add(1,
            new KeyValuePair<string, object?>("shard.id", shardId));
    }
}