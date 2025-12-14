using Shardis.Model;

namespace Shardis.Health;

/// <summary>
/// Represents a health check report for a specific shard.
/// </summary>
public sealed record ShardHealthReport
{
    /// <summary>
    /// Gets the shard identifier.
    /// </summary>
    public required ShardId ShardId { get; init; }

    /// <summary>
    /// Gets the current health status.
    /// </summary>
    public required ShardHealthStatus Status { get; init; }

    /// <summary>
    /// Gets the timestamp when this health report was generated.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets an optional description or reason for the health status.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets an optional exception that caused the unhealthy status.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets the duration of the last health check probe in milliseconds.
    /// </summary>
    public double? ProbeDurationMs { get; init; }
}
