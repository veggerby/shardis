namespace Shardis.Querying.Linq;

/// <summary>
/// Represents configuration options for sharded queries, such as concurrency and tracing.
/// </summary>
public sealed record ShardQueryOptions
{
    /// <summary>
    /// Gets or sets the maximum number of shards to query concurrently.
    /// </summary>
    public int? MaxShardConcurrency { get; init; }

    /// <summary>
    /// Gets or sets the cancellation token for the query.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether tracing is enabled for the query.
    /// </summary>
    public bool EnableTracing { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether partial failures are allowed.
    /// </summary>
    public bool AllowPartialFailures { get; init; }
}