using Shardis.Hashing;
using Shardis.Instrumentation;
using Shardis.Logging;
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
    private readonly Dictionary<ShardId, TShard> _shardById = [];
    private readonly IShardMapStore<TKey> _shardMapStore;
    private readonly IShardKeyHasher<TKey> _shardKeyHasher;
    private readonly int _replicationFactor;
    private readonly IShardRingHasher _ringHasher;
    private readonly object _lock = new();
    private readonly IShardisMetrics _metrics;
    private readonly IShardisLogger _log;
    private static readonly string RouterName = typeof(ConsistentHashShardRouter<TShard, TKey, TSession>).Name;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsistentHashShardRouter{TShard, TKey, TSession}"/> class.
    /// </summary>
    /// <param name="shardMapStore">The shard map store for managing shard assignments.</param>
    /// <param name="availableShards">The collection of available shards.</param>
    /// <param name="replicationFactor">The replication factor for virtual nodes in the consistent hash ring.</param>
    /// <param name="shardKeyHasher">Deterministic shard key hasher used to compute key positions.</param>
    /// <param name="ringHasher">Optional ring hasher; defaults to <see cref="DefaultShardRingHasher"/>.</param>
    /// <param name="metrics">Optional metrics sink; defaults to no-op.</param>
    /// <param name="logger">Optional logger for diagnostics (e.g. ring collision warnings).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shardMapStore"/> or <paramref name="availableShards"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="replicationFactor"/> is less than or equal to zero or <paramref name="availableShards"/> is empty.</exception>
    /// <exception cref="ShardRoutingException">Thrown when <paramref name="replicationFactor"/> exceeds 10,000 or duplicate shard IDs are detected.</exception>
    public ConsistentHashShardRouter(
        IShardMapStore<TKey> shardMapStore,
        IEnumerable<TShard> availableShards,
    IShardKeyHasher<TKey> shardKeyHasher,
    int replicationFactor = 100,
    IShardRingHasher? ringHasher = null,
    IShardisMetrics? metrics = null,
    IShardisLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(shardMapStore, nameof(shardMapStore));
        ArgumentNullException.ThrowIfNull(availableShards, nameof(availableShards));
        ArgumentNullException.ThrowIfNull(shardKeyHasher, nameof(shardKeyHasher));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(replicationFactor, nameof(replicationFactor));

        _shardMapStore = shardMapStore;
        _shardKeyHasher = shardKeyHasher;

        if (replicationFactor > 10_000)
        {
            throw new ShardRoutingException(
                "ReplicationFactor greater than 10,000 is not supported (pathological ring size).",
                null,
                null,
                null,
                null,
                new Dictionary<string, object?> { ["ReplicationFactor"] = replicationFactor });
        }

        _replicationFactor = replicationFactor;
        _ringHasher = ringHasher ?? DefaultShardRingHasher.Instance;
        _metrics = metrics ?? NoOpShardisMetrics.Instance;
        _log = logger ?? NullShardisLogger.Instance;

        var shardList = availableShards.ToList();

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shardList.Count, nameof(availableShards));

        var seen = new HashSet<ShardId>();
        foreach (var shard in shardList)
        {
            if (!seen.Add(shard.ShardId))
            {
                throw new ShardRoutingException(
                    $"Duplicate shard ID detected: {shard.ShardId.Value}",
                    null,
                    shard.ShardId,
                    null,
                    null,
                    null);
            }

            AddShardToRingInternal(shard);
        }

        RebuildKeySnapshot();
    }

    /// <summary>
    /// Dynamically adds a shard to the ring and atomically swaps the key snapshot. Thread-safe.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shard"/> is null.</exception>
    /// <exception cref="ShardRoutingException">Thrown when the shard ID already exists in the ring.</exception>
    public void AddShard(TShard shard)
    {
        ArgumentNullException.ThrowIfNull(shard);
        lock (_lock)
        {
            if (_shardById.ContainsKey(shard.ShardId))
            {
                throw new ShardRoutingException(
                    $"Shard with id {shard.ShardId.Value} already exists.",
                    null,
                    shard.ShardId,
                    null,
                    _shardById.Count,
                    null);
            }

            AddShardToRingInternal(shard);
            RebuildKeySnapshot();
        }
    }

    /// <summary>
    /// Removes a shard from the ring if present and atomically swaps the key snapshot.
    /// Only the virtual nodes belonging to the removed shard are deleted; the remaining ring entries are untouched.
    /// Keys previously assigned remain mapped via the map store until migrated.
    /// </summary>
    /// <returns><c>true</c> if the shard was removed; otherwise <c>false</c>.</returns>
    public bool RemoveShard(ShardId shardId)
    {
        lock (_lock)
        {
            if (!_shardById.Remove(shardId, out _))
            {
                return false;
            }

            // Incremental removal: delete only the virtual nodes that belonged to the removed shard,
            // rather than clearing and rebuilding the entire ring (O(replicationFactor) vs O(n * replicationFactor)).
            var keysToRemove = new List<uint>(_replicationFactor);
            foreach (var kvp in _ring)
            {
                if (kvp.Value.ShardId == shardId)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _ring.Remove(key);
            }

            RebuildKeySnapshot();

            return true;
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
        var (shard, _) = Resolve(shardKey);
        return shard;
    }

    /// <summary>
    /// Adds a shard to the consistent hash ring with virtual nodes.
    /// Hash collisions between virtual nodes are resolved by linear probing (incrementing the hash)
    /// so every virtual node is placed rather than silently dropped.
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

            // Linear probe to resolve collisions so no virtual node is silently lost.
            while (_ring.ContainsKey(hash))
            {
                _log.Log(ShardisLogLevel.Warning, $"[ConsistentHashShardRouter] Virtual node hash collision at {hash:X8} for shard '{shard.ShardId.Value}' replica {i}; probing next slot.");
                unchecked { hash++; }
            }

            _ring[hash] = shard;
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

        using var activity = Diagnostics.ShardisDiagnostics.ActivitySource.StartActivity("shardis.route", System.Diagnostics.ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("shardis.router", RouterName);
            activity.SetTag("shardis.key.hash", _shardKeyHasher.ComputeHash(shardKey).ToString("X8"));
            activity.SetTag("shardis.shard.count", _shardById.Count);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (_shardMapStore.TryGetShardIdForKey(shardKey, out var assignedShardId) && _shardById.TryGetValue(assignedShardId, out var existingShard))
        {
            _metrics.RouteHit(RouterName, existingShard.ShardId.Value, true);
            sw.Stop();
            _metrics.RecordRouteLatency(sw.Elapsed.TotalMilliseconds);
            activity?.SetTag("shardis.assignment.existing", true);
            activity?.SetTag("shardis.route.latency.ms", sw.Elapsed.TotalMilliseconds);
            return (existingShard, true);
        }

        TShard PickShard()
        {
            var keyHash = _shardKeyHasher.ComputeHash(shardKey);
            lock (_lock)
            {
                var keys = _ringKeys;

                if (keys.Length == 0)
                {
                    throw new ShardRoutingException(
                        "Consistent hash ring is empty.",
                        null,
                        null,
                        keyHash,
                        0,
                        null);
                }

                int idx = Array.BinarySearch(keys, keyHash);

                if (idx < 0)
                {
                    idx = ~idx;
                    if (idx == keys.Length) idx = 0;
                }

                var ringKey = keys[idx];

                return _ring[ringKey];
            }
        }

        bool created = _shardMapStore.TryGetOrAdd(shardKey, () => PickShard().ShardId, out var map);

        if (created)
        {
            _metrics.RouteMiss(RouterName);
        }

        if (!_shardById.TryGetValue(map.ShardId, out var resolvedShard))
        {
            // Mapping refers to removed shard; re-pick and force assign
            var replacement = PickShard();
            _shardMapStore.AssignShardToKey(shardKey, replacement.ShardId);
            resolvedShard = replacement;
            created = true;
        }

        _metrics.RouteHit(RouterName, resolvedShard.ShardId.Value, !created);
        sw.Stop();
        _metrics.RecordRouteLatency(sw.Elapsed.TotalMilliseconds);
        activity?.SetTag("shardis.assignment.existing", !created);
        activity?.SetTag("shardis.shard.id", resolvedShard.ShardId.Value);
        activity?.SetTag("shardis.route.latency.ms", sw.Elapsed.TotalMilliseconds);

        return (resolvedShard, !created);
    }
}

