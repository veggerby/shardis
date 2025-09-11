using System.Diagnostics.Metrics;

namespace Shardis.Query.Diagnostics;

/// <summary>Default implementation of <see cref="IShardisQueryMetrics"/> emitting an OpenTelemetry histogram.</summary>
public sealed class MetricShardisQueryMetrics : IShardisQueryMetrics
{
    private static readonly Meter Meter = new(Shardis.Diagnostics.ShardisDiagnostics.MeterName, "1.0.0");
    private static readonly Histogram<double> MergeLatency = Meter.CreateHistogram<double>("shardis.query.merge.latency", unit: "ms", description: "End-to-end duration of merged shard query enumeration");

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
}