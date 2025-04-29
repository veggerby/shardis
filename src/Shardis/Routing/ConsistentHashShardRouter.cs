using System.Security.Cryptography;
using System.Text;

using Shardis.Hashing;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Routing;

/// <summary>
/// Routes ShardKeys to Shards using consistent hashing, minimizing key movement when shards are added or removed.
/// </summary>
public class ConsistentHashShardRouter<TShard, TKey, TSession> : IShardRouter<TKey, TSession>
    where TShard : IShard<TSession>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly SortedDictionary<uint, TShard> _ring = [];
    private readonly IShardMapStore<TKey> _shardMapStore;
    private readonly IShardKeyHasher<TKey> _shardKeyHasher;
    private readonly int _replicationFactor;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsistentHashShardRouter{TSession}"/> class.
    /// </summary>
    /// <param name="shardMapStore">The shard map store for managing shard assignments.</param>
    /// <param name="availableShards">The collection of available shards.</param>
    /// <param name="replicationFactor">The replication factor for virtual nodes in the consistent hash ring.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shardMapStore"/> or <paramref name="availableShards"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="replicationFactor"/> is less than or equal to zero.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="availableShards"/> is empty.</exception>
    public ConsistentHashShardRouter(
        IShardMapStore<TKey> shardMapStore,
        IEnumerable<TShard> availableShards,
        IShardKeyHasher<TKey> shardKeyHasher,
        int replicationFactor = 100)
    {
        ArgumentNullException.ThrowIfNull(shardMapStore, nameof(shardMapStore));
        ArgumentNullException.ThrowIfNull(availableShards, nameof(availableShards));
        ArgumentNullException.ThrowIfNull(shardKeyHasher, nameof(shardKeyHasher));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(replicationFactor, nameof(replicationFactor));

        _shardMapStore = shardMapStore;
        _shardKeyHasher = shardKeyHasher;
        _replicationFactor = replicationFactor;

        var shardList = availableShards.ToList();

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardList.Count(), nameof(availableShards));

        foreach (var shard in shardList)
        {
            AddShardToRing(shard);
        }
    }

    /// <summary>
    /// Routes a given shard key to the appropriate shard using consistent hashing.
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
            var shard = _ring.Values.FirstOrDefault(s => s.ShardId == assignedShardId);
            if (shard != null)
            {
                return shard;
            }
        }

        // Otherwise, route via ring
        var keyHash = _shardKeyHasher.ComputeHash(shardKey);

        IShard<TSession> selectedShard;
        lock (_lock) // Ensure thread safety
        {
            if (_ring.ContainsKey(keyHash))
            {
                selectedShard = _ring[keyHash];
            }
            else
            {
                var next = _ring.Keys.FirstOrDefault(h => h > keyHash);

                if (next == 0)
                {
                    // Wrap around
                    next = _ring.Keys.First();
                }

                selectedShard = _ring[next];
            }
        }

        _shardMapStore.AssignShardToKey(shardKey, selectedShard.ShardId);
        return selectedShard;
    }

    /// <summary>
    /// Adds a shard to the consistent hash ring with virtual nodes.
    /// </summary>
    /// <param name="shard">The shard to add to the ring.</param>
    private void AddShardToRing(TShard shard)
    {
        ArgumentNullException.ThrowIfNull(shard, nameof(shard));

        lock (_lock)
        {
            for (int i = 0; i < _replicationFactor; i++)
            {
                var virtualKey = $"{shard.ShardId}-replica-{i}";
                var hash = ShardHasher.HashString(virtualKey);

                // Ensure no collision on ring (extremely rare with a good hash)
                if (!_ring.ContainsKey(hash))
                {
                    _ring[hash] = shard;
                }
                else
                {
                    // Optional: log warning or retry with salt
                    // Could increment i or retry with `-collision-{guid}` suffix if needed
                }
            }
        }
    }
}
