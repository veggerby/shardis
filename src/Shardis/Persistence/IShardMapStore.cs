using Shardis.Model;

namespace Shardis.Persistence;

/// <summary>
/// Defines the contract for a shard map store that manages key-to-shard assignments.
/// </summary>
public interface IShardMapStore<TKey> where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Attempts to retrieve the shard ID for a given shard key.
    /// </summary>
    /// <param name="shardKey">The shard key to look up.</param>
    /// <param name="shardId">When this method returns, contains the shard ID associated with the key, if found.</param>
    /// <returns><c>true</c> if the shard ID was found; otherwise, <c>false</c>.</returns>
    bool TryGetShardIdForKey(ShardKey<TKey> shardKey, out ShardId shardId);

    /// <summary>
    /// Assigns a shard ID to a given shard key.
    /// </summary>
    /// <param name="shardKey">The shard key to assign.</param>
    /// <param name="shardId">The shard ID to assign to the key.</param>
    /// <returns>A <see cref="ShardMap"/> representing the key-to-shard assignment.</returns>
    ShardMap<TKey> AssignShardToKey(ShardKey<TKey> shardKey, ShardId shardId);
}
