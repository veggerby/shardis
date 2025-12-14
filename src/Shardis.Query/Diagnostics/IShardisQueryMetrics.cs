namespace Shardis.Query.Diagnostics;

/// <summary>Optional metrics sink for query merge latency histogram and health metrics.</summary>
public interface IShardisQueryMetrics
{
    /// <summary>Record end-to-end merged enumeration latency (milliseconds) along with stable tag set.</summary>
    void RecordQueryMergeLatency(double milliseconds, in QueryMetricTags tags);

    /// <summary>Record health probe latency (milliseconds) for a specific shard.</summary>
    void RecordHealthProbeLatency(double milliseconds, string shardId, string status);

    /// <summary>Increment counter for unhealthy shard count.</summary>
    void RecordUnhealthyShardCount(int count);

    /// <summary>Record shard skip event during query execution.</summary>
    void RecordShardSkipped(string shardId, string reason);

    /// <summary>Record shard recovery event.</summary>
    void RecordShardRecovered(string shardId);
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
    /// <summary>Number of invalid shard ids supplied (ignored), 0 when none.</summary>
    public readonly int InvalidShardCount;
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
                           string? rootType,
                           int invalidShardCount)
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
        InvalidShardCount = invalidShardCount;
    }
}