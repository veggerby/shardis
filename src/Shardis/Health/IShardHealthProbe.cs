using Shardis.Model;

namespace Shardis.Health;

/// <summary>
/// Defines how to perform a health check probe on a shard.
/// </summary>
/// <remarks>
/// Implementations are provider-specific (e.g., EF Core, Marten, Redis) and encapsulate
/// the logic to verify shard connectivity and basic functionality.
/// </remarks>
public interface IShardHealthProbe
{
    /// <summary>
    /// Executes a health probe against the specified shard.
    /// </summary>
    /// <param name="shardId">The shard identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A health report indicating the probe result.</returns>
    ValueTask<ShardHealthReport> ExecuteAsync(ShardId shardId, CancellationToken ct = default);
}
