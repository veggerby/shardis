using Shardis.Hashing;
using Shardis.Instrumentation;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Routing;

/// <summary>
/// Consistent hash ring router minimizing key movement (churn) when shards are added or removed.
/// </summary>
/// <remarks>
/// Each shard is represented by <see cref="_replicationFactor"/> virtual nodes on the ring. Lookups compute a key hash and
/// select the first ring entry clockwise (wrapping to the first node if necessary). Assignments are persisted in the map store
/// to ensure stable resolution across process restarts.
/// </remarks>
public class ConsistentHashShardRouter<TShard, TKey, TSession> : IShardRouter<TKey, TSession>
    where TShard : IShard<TSession>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly SortedDictionary<uint, TShard> _ring = [];
    private uint[] _ringKeys = Array.Empty<uint>(); // snapshot for binary search
    private readonly Dictionary<ShardId, TShard> _shardById = new();
    private readonly IShardMapStore<TKey> _shardMapStore;
    private readonly IShardKeyHasher<TKey> _shardKeyHasher;
    private readonly int _replicationFactor;
    private readonly IShardRingHasher _ringHasher;
    private readonly object _lock = new();
    private readonly IShardisMetrics _metrics;
    private static readonly string RouterName = typeof(ConsistentHashShardRouter<TShard, TKey, TSession>).Name;

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
    int replicationFactor = 100,
    IShardRingHasher? ringHasher = null,
    IShardisMetrics? metrics = null)
    {
        ArgumentNullException.ThrowIfNull(shardMapStore, nameof(shardMapStore));
        ArgumentNullException.ThrowIfNull(availableShards, nameof(availableShards));
        ArgumentNullException.ThrowIfNull(shardKeyHasher, nameof(shardKeyHasher));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(replicationFactor, nameof(replicationFactor));

        _shardMapStore = shardMapStore;
        _shardKeyHasher = shardKeyHasher;
        _replicationFactor = replicationFactor;
        _ringHasher = ringHasher ?? DefaultShardRingHasher.Instance;
        _metrics = metrics ?? NoOpShardisMetrics.Instance;

        var shardList = availableShards.ToList();

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardList.Count(), nameof(availableShards));

        var seen = new HashSet<ShardId>();
        foreach (var shard in shardList)
        {
            if (!seen.Add(shard.ShardId))
            {
                throw new InvalidOperationException($"Duplicate shard ID detected: {shard.ShardId.Value}");
            }
            AddShardToRingInternal(shard);
        }
        RebuildKeySnapshot();
    }

    /// <summary>
    /// Routes a given shard key to the appropriate shard using consistent hashing.
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
    /// Adds a shard to the consistent hash ring with virtual nodes.
    /// </summary>
    /// <param name="shard">The shard to add to the ring.</param>
    private void AddShardToRingInternal(TShard shard)
    {
        ArgumentNullException.ThrowIfNull(shard);
        _shardById[shard.ShardId] = shard;
        for (int i = 0; i < _replicationFactor; i++)
        {
            var virtualKey = $"{shard.ShardId}-replica-{i}";
            var hash = _ringHasher.Hash(virtualKey);
            if (!_ring.ContainsKey(hash))
            {
                _ring[hash] = shard;
            }
        }
    }

    private void RebuildKeySnapshot()
    {
        _ringKeys = _ring.Keys.ToArray();
    }

    /// <summary>
    /// Routes and returns a richer assignment result indicating whether the key already had an assignment.
    /// </summary>
    public ShardAssignmentResult<TSession> Route(ShardKey<TKey> shardKey)
    {
        var (shard, existing) = Resolve(shardKey);
        return new(shard, existing);
    }

    private (IShard<TSession> shard, bool existing) Resolve(ShardKey<TKey> shardKey)
    {
        if (shardKey.Value == null) throw new ArgumentNullException(nameof(shardKey));

        if (_shardMapStore.TryGetShardIdForKey(shardKey, out var assignedShardId) && _shardById.TryGetValue(assignedShardId, out var existingShard))
        {
            _metrics.RouteHit(RouterName, existingShard.ShardId.Value, true);
            return (existingShard, true);
        }

        _metrics.RouteMiss(RouterName);
        var keyHash = _shardKeyHasher.ComputeHash(shardKey);
        TShard selected;
        lock (_lock)
        {
            // Use snapshot for binary search to minimize lock duration
            var keys = _ringKeys;
            if (keys.Length == 0)
            {
                throw new InvalidOperationException("Consistent hash ring is empty.");
            }
            int idx = Array.BinarySearch(keys, keyHash);
            if (idx < 0)
            {
                idx = ~idx; // next larger
                if (idx == keys.Length) idx = 0; // wrap-around
            }
            var ringKey = keys[idx];
            selected = _ring[ringKey];
        }
        _shardMapStore.TryAssignShardToKey(shardKey, selected.ShardId, out _);
        _metrics.RouteHit(RouterName, selected.ShardId.Value, false);
        return (selected, false);
    }
}