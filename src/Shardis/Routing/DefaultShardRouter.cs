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
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ShardKey<TKey>, byte> _missRecorded = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ShardKey<TKey>, object> _keyLocks = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultShardRouter{TKey, TSession}"/> class.
    /// </summary>
    /// <param name="shardMapStore">The shard map store for managing shard assignments.</param>
    /// <param name="availableShards">The collection of available shards.</param>
    /// <param name="shardKeyHasher">Optional custom key hasher; defaults to <see cref="DefaultShardKeyHasher{TKey}"/>.</param>
    /// <param name="metrics">Optional metrics sink; defaults to no-op.</param>
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
        // start an activity for routing
        using var activity = Shardis.Diagnostics.ShardisDiagnostics.ActivitySource.StartActivity("shardis.route", System.Diagnostics.ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("shardis.router", RouterName);
            // safe short token for key (hex)
            activity.SetTag("shardis.key.hash", _shardKeyHasher.ComputeHash(shardKey).ToString("X8"));
            activity.SetTag("shardis.shard.count", _availableShards.Count);
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

        var keyLock = _keyLocks.GetOrAdd(shardKey, _ => new object());
        bool existing;
        IShard<TSession> shard;
        lock (keyLock)
        {
            if (_shardMapStore.TryGetShardIdForKey(shardKey, out var existingId) && _shardById.TryGetValue(existingId, out var existingShard2))
            {
                shard = existingShard2;
                existing = true;
            }
            else
            {
                var idx = CalculateShardIndex(shardKey, _availableShards.Count);
                shard = _availableShards[(int)idx];
                var created = _shardMapStore.TryAssignShardToKey(shardKey, shard.ShardId, out _);

                if (created && _missRecorded.TryAdd(shardKey, 0))
                {
                    _metrics.RouteMiss(RouterName);
                }

                existing = !created;
            }
        }

        _metrics.RouteHit(RouterName, shard.ShardId.Value, existing);
        sw.Stop();
        _metrics.RecordRouteLatency(sw.Elapsed.TotalMilliseconds);
        activity?.SetTag("shardis.assignment.existing", existing);
        activity?.SetTag("shardis.shard.id", shard.ShardId.Value);
        activity?.SetTag("shardis.route.latency.ms", sw.Elapsed.TotalMilliseconds);
        return (shard, existing);
    }
}