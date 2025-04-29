using System.Security.Cryptography;
using System.Text;

using Shardis.Hashing;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Routing;

/// <summary>
/// Provides a default shard routing implementation using a simple hash-based strategy.
/// </summary>
public class DefaultShardRouter<TKey, TSession> : IShardRouter<TKey, TSession>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// The list of available shards.
    /// </summary>
    private readonly List<IShard<TSession>> _availableShards;

    /// <summary>
    /// The shard map store for managing shard assignments.
    /// </summary>
    private readonly IShardMapStore<TKey> _shardMapStore;
    private readonly IShardKeyHasher<TKey> _shardKeyHasher;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultShardRouter{TSession}"/> class.
    /// </summary>
    /// <param name="shardMapStore">The shard map store for managing shard assignments.</param>
    /// <param name="availableShards">The collection of available shards.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shardMapStore"/> or <paramref name="availableShards"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="availableShards"/> is empty.</exception>
    public DefaultShardRouter(
        IShardMapStore<TKey> shardMapStore,
        IEnumerable<IShard<TSession>> availableShards,
        IShardKeyHasher<TKey>? shardKeyHasher = null)
    {
        ArgumentNullException.ThrowIfNull(shardMapStore, nameof(shardMapStore));
        ArgumentNullException.ThrowIfNull(availableShards, nameof(availableShards));

        _shardMapStore = shardMapStore;
        _shardKeyHasher = shardKeyHasher ?? DefaultShardKeyHasher<TKey>.Instance;
        _availableShards = availableShards.ToList();

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_availableShards.Count, nameof(availableShards));
    }

    /// <summary>
    /// Routes a given shard key to the appropriate shard using a hash-based strategy.
    /// </summary>
    /// <param name="shardKey">The shard key representing an aggregate instance.</param>
    /// <returns>The shard that should handle the given key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shardKey"/> is null.</exception>
    public IShard<TSession> RouteToShard(ShardKey<TKey> shardKey)
    {
        if (shardKey.Value == null) throw new ArgumentNullException(nameof(shardKey));

        // First check if the key has already been assigned
        if (_shardMapStore.TryGetShardIdForKey(shardKey, out var assignedShardId))
        {
            var existingShard = _availableShards.FirstOrDefault(s => s.ShardId == assignedShardId);
            if (existingShard != null)
            {
                return existingShard;
            }
        }

        // If no assignment exists, hash it and assign
        var shardIndex = CalculateShardIndex(shardKey, _availableShards.Count);
        var selectedShard = _availableShards[(int)shardIndex];

        // Store the assignment
        _shardMapStore.AssignShardToKey(shardKey, selectedShard.ShardId);

        return selectedShard;
    }

    /// <summary>
    /// Calculates the index of the shard for a given key value.
    /// </summary>
    /// <param name="keyValue">The key value to hash.</param>
    /// <param name="shardCount">The total number of available shards.</param>
    /// <returns>The index of the shard.</returns>
    private long CalculateShardIndex(ShardKey<TKey> keyValue, int shardCount)
    {
        // Ensure positive integer
        var hash = _shardKeyHasher.ComputeHash(keyValue);
        return hash % shardCount;
    }
}
