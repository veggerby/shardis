namespace Shardis.Query.Diagnostics;

/// <summary>Optional metrics sink for query merge latency histogram.</summary>
public interface IShardisQueryMetrics
{
    /// <summary>Record end-to-end merged enumeration latency (milliseconds) along with stable tag set.</summary>
    void RecordQueryMergeLatency(double milliseconds, in QueryMetricTags tags);
}

/// <summary>Stable tag set for query latency metrics.</summary>
public readonly struct QueryMetricTags
{
    /// <summary>Database system (e.g. postgresql, mssql, sqlite).</summary>
    public readonly string? DbSystem;
    /// <summary>Logical query provider (e.g. efcore, inmemory).</summary>
    public readonly string? Provider;
    /// <summary>Total configured logical shard count.</summary>
    public readonly int ShardCount;
    /// <summary>Number of shards actually targeted by this query (fan-out reduction).</summary>
    public readonly int TargetShardCount;
    /// <summary>Merge strategy identifier (unordered | ordered).</summary>
    public readonly string? MergeStrategy;
    /// <summary>Indicates whether global ordering required buffering (true | false).</summary>
    public readonly string? OrderingBuffered;
    /// <summary>Effective parallel fan-out concurrency used.</summary>
    public readonly int FanoutConcurrency;
    /// <summary>Channel capacity used for merge (-1 when unbounded).</summary>
    public readonly int ChannelCapacity;
    /// <summary>Failure handling mode (fail-fast | best-effort).</summary>
    public readonly string? FailureMode;
    /// <summary>Result status (ok | canceled | failed).</summary>
    public readonly string? ResultStatus;
    /// <summary>Simple name of root element type.</summary>
    public readonly string? RootType;
    /// <summary>Create a new tag set instance.</summary>
    public QueryMetricTags(string? dbSystem,
                           string? provider,
                           int shardCount,
                           int targetShardCount,
                           string? mergeStrategy,
                           string? orderingBuffered,
                           int fanoutConcurrency,
                           int channelCapacity,
                           string? failureMode,
                           string? resultStatus,
                           string? rootType)
    {
        DbSystem = dbSystem;
        Provider = provider;
        ShardCount = shardCount;
        TargetShardCount = targetShardCount;
        MergeStrategy = mergeStrategy;
        OrderingBuffered = orderingBuffered;
        FanoutConcurrency = fanoutConcurrency;
        ChannelCapacity = channelCapacity;
        FailureMode = failureMode;
        ResultStatus = resultStatus;
        RootType = rootType;
    }
}