using Shardis.Model;

namespace Shardis.Factories;

/// <summary>
/// General-purpose factory abstraction for creating shard-scoped resources (e.g. DbContext, IDocumentSession, IDatabase).
/// </summary>
/// <typeparam name="T">The shard-scoped resource type.</typeparam>
public interface IShardFactory<T>
{
    /// <summary>
    /// Synchronously creates a shard-scoped resource. Override when creation is cheap and does not require async I/O.
    /// </summary>
    /// <param name="shard">The logical shard identifier.</param>
    /// <returns>The created resource.</returns>
    /// <exception cref="NotSupportedException">When not overridden by implementation.</exception>
    T Create(ShardId shard) => throw new NotSupportedException("Synchronous Create not supported - use CreateAsync.");

    /// <summary>
    /// Asynchronously creates a shard-scoped resource. Implementations may return a completed <see cref="ValueTask{TResult}"/> when creation is synchronous.
    /// </summary>
    /// <param name="shard">The logical shard identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A value task representing the asynchronous creation.</returns>
    ValueTask<T> CreateAsync(ShardId shard, CancellationToken ct = default);
}