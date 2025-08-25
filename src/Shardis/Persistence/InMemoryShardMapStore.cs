using System.Collections.Concurrent;

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
    private readonly ConcurrentDictionary<ShardKey<TKey>, ShardId> _assignments = new();

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

    /// <inheritdoc />
    public bool TryAssignShardToKey(ShardKey<TKey> shardKey, ShardId shardId, out ShardMap<TKey> shardMap)
    {
        var added = _assignments.TryAdd(shardKey, shardId);
        var effective = _assignments[shardKey];
        shardMap = new ShardMap<TKey>(shardKey, effective);
        return added;
    }

    /// <inheritdoc />
    public bool TryGetOrAdd(ShardKey<TKey> shardKey, Func<ShardId> valueFactory, out ShardMap<TKey> shardMap)
    {
        ArgumentNullException.ThrowIfNull(valueFactory, nameof(valueFactory));

        if (_assignments.TryGetValue(shardKey, out var existing))
        {
            shardMap = new ShardMap<TKey>(shardKey, existing);
            return false;
        }

        // Compute candidate id outside add for deterministic hashing cost per contender.
        var candidate = valueFactory();
        if (_assignments.TryAdd(shardKey, candidate))
        {
            shardMap = new ShardMap<TKey>(shardKey, candidate);
            return true;
        }

        // Lost race: fetch existing (must succeed)
        var winner = _assignments[shardKey];
        shardMap = new ShardMap<TKey>(shardKey, winner);
        return false;
    }
}