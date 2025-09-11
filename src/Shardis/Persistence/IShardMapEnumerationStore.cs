using Shardis.Model;

namespace Shardis.Persistence;

/// <summary>
/// Optional extension of <see cref="IShardMapStore{TKey}"/> that supports streaming enumeration of existing key→shard assignments.
/// Implementations should provide a point-in-time (best effort) view. Backends may page internally.
/// </summary>
/// <remarks>
/// This interface is additive and NOT required for routing. Migration helpers can use it to build a point-in-time snapshot.
/// Implementations should: (1) honor cancellation, (2) avoid unbounded memory / locks, and (3) clearly document consistency semantics.
/// </remarks>
public interface IShardMapEnumerationStore<TKey> : IShardMapStore<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Asynchronously enumerates all known shard key assignments.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A sequence of <see cref="ShardMap{TKey}"/> records.</returns>
    IAsyncEnumerable<ShardMap<TKey>> EnumerateAsync(CancellationToken cancellationToken = default);
}