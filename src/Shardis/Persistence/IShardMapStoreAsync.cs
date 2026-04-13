using Shardis.Model;

namespace Shardis.Persistence;

/// <summary>
/// Defines the async contract for a shard map store that manages key-to-shard assignments.
/// Prefer this interface for implementations that perform I/O (database, cache, etc.).
/// </summary>
public interface IShardMapStoreAsync<TKey> where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Attempts to retrieve the shard ID for a given shard key asynchronously.
    /// </summary>
    /// <param name="shardKey">The shard key to look up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The shard ID if found; otherwise, null.</returns>
    ValueTask<ShardId?> TryGetShardIdForKeyAsync(ShardKey<TKey> shardKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a shard ID to a given shard key asynchronously.
    /// </summary>
    /// <param name="shardKey">The shard key to assign.</param>
    /// <param name="shardId">The shard ID to assign to the key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ShardMap{TKey}"/> representing the key-to-shard assignment.</returns>
    ValueTask<ShardMap<TKey>> AssignShardToKeyAsync(ShardKey<TKey> shardKey, ShardId shardId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to assign the shard ID to the given shard key only if no existing assignment is present.
    /// </summary>
    /// <param name="shardKey">The shard key to assign.</param>
    /// <param name="shardId">The shard ID to attempt to assign.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing: (1) true if the assignment was created by this call, false if an assignment already existed; (2) the resulting mapping (existing or newly added).</returns>
    ValueTask<(bool Created, ShardMap<TKey> ShardMap)> TryAssignShardToKeyAsync(ShardKey<TKey> shardKey, ShardId shardId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to get the existing shard assignment or atomically create it using the provided factory when absent.
    /// </summary>
    /// <param name="shardKey">The shard key.</param>
    /// <param name="valueFactory">Factory invoked to obtain a shard id when the key is not yet assigned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing: (1) true if the mapping was created during this call, otherwise false; (2) resulting mapping (existing or newly created).</returns>
    ValueTask<(bool Created, ShardMap<TKey> ShardMap)> TryGetOrAddAsync(ShardKey<TKey> shardKey, Func<ShardId> valueFactory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to get the existing shard assignment or atomically create it using the provided asynchronous factory when absent.
    /// </summary>
    /// <param name="shardKey">The shard key.</param>
    /// <param name="asyncValueFactory">Asynchronous factory invoked to obtain a shard id when the key is not yet assigned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing: (1) true if the mapping was created during this call, otherwise false; (2) resulting mapping (existing or newly created).</returns>
    /// <remarks>
    /// The default implementation is non-atomic (uses a get-then-set pattern with a TOCTOU window).
    /// Implementations backed by atomic stores (Redis NX, SQL INSERT-if-not-exists, etc.) should override this.
    /// </remarks>
    async ValueTask<(bool Created, ShardMap<TKey> ShardMap)> TryGetOrAddAsync(
        ShardKey<TKey> shardKey,
        Func<CancellationToken, ValueTask<ShardId>> asyncValueFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asyncValueFactory);
        var existing = await TryGetShardIdForKeyAsync(shardKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return (false, new ShardMap<TKey>(shardKey, existing.Value));
        }

        var id = await asyncValueFactory(cancellationToken).ConfigureAwait(false);
        return await TryAssignShardToKeyAsync(shardKey, id, cancellationToken).ConfigureAwait(false);
    }
}
