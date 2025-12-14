using Shardis;
using Shardis.Model;

namespace Shardis.Query.Health;

/// <summary>
/// Exception thrown when shard availability requirements are not met during query execution.
/// </summary>
public sealed class InsufficientHealthyShardsException : ShardisException
{
    /// <summary>
    /// Gets the total number of targeted shards.
    /// </summary>
    public int TotalShards { get; }

    /// <summary>
    /// Gets the number of healthy shards available.
    /// </summary>
    public int HealthyShards { get; }

    /// <summary>
    /// Gets the collection of unhealthy shard IDs.
    /// </summary>
    public IReadOnlyList<ShardId> UnhealthyShardIds { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InsufficientHealthyShardsException"/> class.
    /// </summary>
    public InsufficientHealthyShardsException(
        int totalShards,
        int healthyShards,
        IReadOnlyList<ShardId> unhealthyShardIds,
        string message)
        : base(message)
    {
        TotalShards = totalShards;
        HealthyShards = healthyShards;
        UnhealthyShardIds = unhealthyShardIds;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InsufficientHealthyShardsException"/> class.
    /// </summary>
    public InsufficientHealthyShardsException(
        int totalShards,
        int healthyShards,
        IReadOnlyList<ShardId> unhealthyShardIds,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        TotalShards = totalShards;
        HealthyShards = healthyShards;
        UnhealthyShardIds = unhealthyShardIds;
    }
}
