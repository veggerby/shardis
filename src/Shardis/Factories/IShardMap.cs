using Shardis.Model;

namespace Shardis.Factories;

/// <summary>
/// Provides configuration metadata for shards (e.g. connection strings).
/// </summary>
public interface IShardMap
{
    /// <summary>
    /// Gets the declared shards.
    /// </summary>
    IEnumerable<ShardId> Shards { get; }

    /// <summary>
    /// Resolves the connection string (or provider-specific locator) for a shard.
    /// </summary>
    /// <param name="shard">Shard identifier.</param>
    /// <returns>The connection string.</returns>
    string GetConnectionString(ShardId shard);
}