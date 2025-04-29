using Shardis.Model;

namespace Shardis.Persistence;

/// <summary>
/// Provides an in-memory implementation of the <see cref="IShardMapStore"/> interface.
/// </summary>
public class InMemoryShardMapStore<TKey> : IShardMapStore<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Stores the key-to-shard assignments in memory.
    /// </summary>
    private readonly Dictionary<ShardKey<TKey>, ShardId> _assignments = [];

    /// <summary>
    /// Attempts to retrieve the shard ID for a given shard key.
    /// </summary>
    /// <param name="shardKey">The shard key to look up.</param>
    /// <param name="shardId">When this method returns, contains the shard ID associated with the key, if found.</param>
    /// <returns><c>true</c> if the shard ID was found; otherwise, <c>false</c>.</returns>
    public bool TryGetShardIdForKey(ShardKey<TKey> shardKey, out ShardId shardId) => _assignments.TryGetValue(shardKey, out shardId);

    /// <summary>
    /// Assigns a shard ID to a given shard key.
    /// </summary>
    /// <param name="shardKey">The shard key to assign.</param>
    /// <param name="shardId">The shard ID to assign to the key.</param>
    /// <returns>A <see cref="ShardMap"/> representing the key-to-shard assignment.</returns>
    public ShardMap<TKey> AssignShardToKey(ShardKey<TKey> shardKey, ShardId shardId)
    {
        _assignments[shardKey] = shardId;
        return new ShardMap<TKey>(shardKey, shardId);
    }
}