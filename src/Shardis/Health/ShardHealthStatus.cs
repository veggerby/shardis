namespace Shardis.Health;

/// <summary>
/// Represents the health status of a shard.
/// </summary>
public enum ShardHealthStatus
{
    /// <summary>
    /// Health status is not yet determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Shard is healthy and available for operations.
    /// </summary>
    Healthy = 1,

    /// <summary>
    /// Shard is degraded but may still serve requests with reduced quality.
    /// </summary>
    Degraded = 2,

    /// <summary>
    /// Shard is unhealthy and should not receive new requests.
    /// </summary>
    Unhealthy = 3
}
