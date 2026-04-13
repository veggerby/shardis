using Shardis.Hashing;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;

namespace Shardis;

/// <summary>
/// Options controlling Shardis registration and router behavior.
/// </summary>
/// <typeparam name="TShard">Shard implementation type.</typeparam>
/// <typeparam name="TKey">Shard key underlying value type.</typeparam>
/// <typeparam name="TSession">Session/context type.</typeparam>
public class ShardisOptions<TShard, TKey, TSession>
    where TShard : IShard<TSession>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Collection of shards to register. Must not be empty when building the provider.
    /// </summary>
    public IList<TShard> Shards { get; } = [];

    /// <summary>
    /// When <c>true</c> a consistent hash ring router is used; when <c>false</c> (default for new installations)
    /// the simpler modulo-based <see cref="DefaultShardRouter{TKey, TSession}"/> is used.
    /// Consistent hashing is recommended when the shard topology changes at runtime (adding/removing shards)
    /// because it minimises key reassignments. For static shard counts the default router has lower overhead.
    /// Ignored if <see cref="RouterFactory"/> is supplied.
    /// </summary>
    public bool UseConsistentHashing { get; set; } = false;

    /// <summary>
    /// Fully custom router factory. If provided it overrides <see cref="UseConsistentHashing"/> selection logic.
    /// </summary>
    public Func<IServiceProvider, IEnumerable<TShard>, IShardRouter<TKey, TSession>>? RouterFactory { get; set; }

    /// <summary>
    /// Factory for a custom shard map store. If null an in-memory implementation is used (unless one was pre-registered).
    /// </summary>
    public Func<IServiceProvider, IShardMapStore<TKey>>? ShardMapStoreFactory { get; set; }

    /// <summary>
    /// Override key hasher; defaults to <see cref="DefaultShardKeyHasher{TKey}"/>.
    /// </summary>
    public IShardKeyHasher<TKey>? ShardKeyHasher { get; set; }

    /// <summary>
    /// Optional ring hasher for consistent hashing (only used when <see cref="UseConsistentHashing"/> is true).
    /// </summary>
    public IShardRingHasher? RingHasher { get; set; }

    /// <summary>
    /// Virtual node replication factor for consistent hashing. Higher values yield smoother distribution with higher memory cost.
    /// </summary>
    public int ReplicationFactor { get; set; } = 100;
}