namespace Shardis.Query.Health;

/// <summary>
/// Defines the behavior when encountering unhealthy shards during query execution.
/// </summary>
public enum UnhealthyShardBehavior
{
    /// <summary>
    /// Include unhealthy shards in the query (default behavior, no health filtering).
    /// </summary>
    Include = 0,

    /// <summary>
    /// Skip unhealthy shards and continue with healthy shards (best-effort mode).
    /// </summary>
    Skip = 1,

    /// <summary>
    /// Quarantine unhealthy shards completely and fail if any targeted shard is unhealthy (strict mode).
    /// </summary>
    Quarantine = 2,

    /// <summary>
    /// Reserved for future use. Not currently implemented.
    /// Intended to degrade query by marking partial results when unhealthy shards are skipped.
    /// </summary>
    Degrade = 3
}
