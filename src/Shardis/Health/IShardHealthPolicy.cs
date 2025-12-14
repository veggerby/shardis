using Shardis.Model;

namespace Shardis.Health;

/// <summary>
/// Defines how shard health is monitored, tracked, and reported.
/// </summary>
/// <remarks>
/// Implementations control probe cadence, failure thresholds, cooldown periods,
/// and recovery logic. The policy is consulted during query execution to determine
/// which shards should be included or excluded.
/// </remarks>
public interface IShardHealthPolicy
{
    /// <summary>
    /// Gets the current health status for a specific shard.
    /// </summary>
    /// <param name="shardId">The shard identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current health report for the shard.</returns>
    ValueTask<ShardHealthReport> GetHealthAsync(ShardId shardId, CancellationToken ct = default);

    /// <summary>
    /// Gets the health status for all shards.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection of health reports for all known shards.</returns>
    IAsyncEnumerable<ShardHealthReport> GetAllHealthAsync(CancellationToken ct = default);

    /// <summary>
    /// Records a successful operation on a shard (optional hook for reactive health tracking).
    /// </summary>
    /// <param name="shardId">The shard identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask RecordSuccessAsync(ShardId shardId, CancellationToken ct = default);

    /// <summary>
    /// Records a failed operation on a shard (optional hook for reactive health tracking).
    /// </summary>
    /// <param name="shardId">The shard identifier.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask RecordFailureAsync(ShardId shardId, Exception exception, CancellationToken ct = default);

    /// <summary>
    /// Triggers an immediate health probe for a specific shard (bypasses scheduled cadence).
    /// </summary>
    /// <param name="shardId">The shard identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated health report.</returns>
    ValueTask<ShardHealthReport> ProbeAsync(ShardId shardId, CancellationToken ct = default);
}
