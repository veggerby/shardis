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

    /// <summary>
    /// Resolves the shard and returns a <see cref="ShardAssignmentResult{TSession}"/> indicating whether the
    /// assignment already existed prior to this call.
    /// </summary>
    /// <param name="shardKey">The logical shard key representing an aggregate instance.</param>
    /// <returns>Assignment result containing the resolved shard and whether it was pre-existing.</returns>
    ShardAssignmentResult<TSession> Route(ShardKey<TKey> shardKey) => new(RouteToShard(shardKey), false);

    /// <summary>
    /// Asynchronously resolves the shard responsible for the supplied <paramref name="shardKey"/>.
    /// The default implementation calls <see cref="RouteToShard"/> synchronously and wraps the result.
    /// Override when the backing store supports async I/O for meaningful async benefit.
    /// </summary>
    /// <param name="shardKey">The logical shard key representing an aggregate instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The shard that should handle the given key.</returns>
    ValueTask<IShard<TSession>> RouteToShardAsync(ShardKey<TKey> shardKey, CancellationToken ct = default) =>
        ValueTask.FromResult(RouteToShard(shardKey));
}