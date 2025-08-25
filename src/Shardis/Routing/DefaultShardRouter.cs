using Shardis.Hashing;
using Shardis.Instrumentation;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Routing;

/// <summary>
/// Default shard router using a direct modulo hash strategy over the configured shard list.
/// </summary>
/// <remarks>
/// This router guarantees sticky assignment after first resolution via the shard map store.
/// Subsequent routes for the same key perform a dictionary lookup prior to hashing, reducing CPU cost.
/// </remarks>
public class DefaultShardRouter<TKey, TSession> : IShardRouter<TKey, TSession>
    where TKey : notnull, IEquatable<TKey>
{
    private static readonly string RouterName = typeof(DefaultShardRouter<TKey, TSession>).Name;
    /// <summary>
    /// The list of available shards.
    /// </summary>
    private readonly List<IShard<TSession>> _availableShards;
    private readonly Dictionary<ShardId, IShard<TSession>> _shardById;

    /// <summary>
    /// The shard map store for managing shard assignments.
    /// </summary>
    private readonly IShardMapStore<TKey> _shardMapStore;
    private readonly IShardKeyHasher<TKey> _shardKeyHasher;
    private readonly IShardisMetrics _metrics;

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
    IShardKeyHasher<TKey>? shardKeyHasher = null,
    IShardisMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(shardMapStore, nameof(shardMapStore));
        ArgumentNullException.ThrowIfNull(availableShards, nameof(availableShards));

        _shardMapStore = shardMapStore;
        _shardKeyHasher = shardKeyHasher ?? DefaultShardKeyHasher<TKey>.Instance;
        _availableShards = availableShards.ToList();
        // Validate uniqueness of shard IDs
        _shardById = new Dictionary<ShardId, IShard<TSession>>();
        foreach (var shard in _availableShards)
        {
            if (_shardById.ContainsKey(shard.ShardId))
            {
                throw new InvalidOperationException($"Duplicate shard ID detected: {shard.ShardId.Value}");
            }
            _shardById[shard.ShardId] = shard;
        }
        _metrics = metrics ?? NoOpShardisMetrics.Instance;

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
        var (shard, _) = Resolve(shardKey);
        return shard;
    }

    /// <summary>
    /// Routes and returns a richer assignment result indicating whether the key already had an assignment.
    /// </summary>
    public ShardAssignmentResult<TSession> Route(ShardKey<TKey> shardKey)
    {
        var (shard, existing) = Resolve(shardKey);
        return new(shard, existing);
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

    private (IShard<TSession> shard, bool existing) Resolve(ShardKey<TKey> shardKey)
    {
        if (shardKey.Value == null)
        {
            throw new ArgumentNullException(nameof(shardKey));
        }

        if (_shardMapStore.TryGetShardIdForKey(shardKey, out var assignedShardId) && _shardById.TryGetValue(assignedShardId, out var existingShard))
        {
            _metrics.RouteHit(RouterName, existingShard.ShardId.Value, true);
            return (existingShard, true);
        }

        _metrics.RouteMiss(RouterName);
        var shardIndex = CalculateShardIndex(shardKey, _availableShards.Count);
        var selectedShard = _availableShards[(int)shardIndex];
        // Attempt CAS assignment to avoid overwriting concurrent assignment if races occur
        _shardMapStore.TryAssignShardToKey(shardKey, selectedShard.ShardId, out _);
        _metrics.RouteHit(RouterName, selectedShard.ShardId.Value, false);
        return (selectedShard, false);
    }
}