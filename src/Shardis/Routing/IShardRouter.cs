using Shardis.Model;

namespace Shardis.Routing;

/// <summary>
/// Defines the contract for routing a logical <see cref="ShardKey{TKey}"/> to a physical shard instance.
/// Implementations must be deterministic and thread-safe; they may leverage hashing strategies and
/// a backing <c>IShardMapStore</c> to preserve sticky assignments.
/// </summary>
/// <typeparam name="TKey">The shard key value type.</typeparam>
/// <typeparam name="TSession">The session type exposed by shards.</typeparam>
public interface IShardRouter<TKey, TSession> where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Resolves the shard responsible for the supplied <paramref name="shardKey"/>.
    /// Implementations should avoid expensive hashing work on hot paths when a prior
    /// assignment already exists in the shard map store.
    /// </summary>
    /// <param name="shardKey">The logical shard key representing an aggregate instance.</param>
    /// <returns>The shard that should handle the given key.</returns>
    IShard<TSession> RouteToShard(ShardKey<TKey> shardKey);
}