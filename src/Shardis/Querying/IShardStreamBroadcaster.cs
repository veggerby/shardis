namespace Shardis.Querying;

/// <summary>
/// Defines the contract for broadcasting queries to all shards and aggregating results as asynchronous streams.
/// </summary>
/// <typeparam name="TSession">The type of session used for querying shards.</typeparam>
public interface IShardStreamBroadcaster<TSession>
{
    /// <summary>
    /// Executes a query on all shards in parallel and aggregates the results as an asynchronous stream.
    /// </summary>
    /// <typeparam name="TResult">The type of the query result.</typeparam>
    /// <param name="query">A function that defines the query to execute on each shard session.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An asynchronous stream containing the aggregated results from all shards.</returns>
    IAsyncEnumerable<ShardItem<TResult>> QueryAllShardsAsync<TResult>(Func<TSession, IAsyncEnumerable<TResult>> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a globally ordered query by first materializing all shard streams then performing a k-way merge.
    /// This is the EAGER mode (high memory, delayed first item) and will be deprecated in favor of explicit streaming APIs.
    /// </summary>
    /// <remarks>Prefer <see cref="QueryAllShardsOrderedStreamingAsync"/> for large result sets to avoid full materialization.</remarks>
    /// <typeparam name="TResult">The type of the query result.</typeparam>
    /// <typeparam name="TKey">The type of the key used for ordering.</typeparam>
    /// <param name="query">Per-shard query delegate producing an async stream.</param>
    /// <param name="keySelector">Key selector used for global ordering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Globally ordered shard items.</returns>
    [Obsolete("Use QueryAllShardsOrderedStreamingAsync (streaming) or QueryAllShardsOrderedEagerAsync (explicit) instead. Will be removed in vNext.")]
    IAsyncEnumerable<ShardItem<TResult>> QueryAllShardsOrderedAsync<TResult, TKey>(
        Func<TSession, IAsyncEnumerable<TResult>> query,
        Func<TResult, TKey> keySelector,
        CancellationToken cancellationToken = default)
        where TKey : IComparable<TKey>;

    /// <summary>
    /// Executes a globally ordered query using a STREAMING k-way merge with bounded memory.
    /// Items are yielded as soon as the next globally minimal key is available across shards.
    /// </summary>
    /// <typeparam name="TResult">Result item type.</typeparam>
    /// <typeparam name="TKey">Ordering key type.</typeparam>
    /// <param name="query">Per-shard query delegate producing an async stream.</param>
    /// <param name="keySelector">Key selector used for ordering.</param>
    /// <param name="prefetchPerShard">Maximum number of buffered (prefetched but not yet yielded) items per shard.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Globally ordered shard items, streamed.</returns>
    IAsyncEnumerable<ShardItem<TResult>> QueryAllShardsOrderedStreamingAsync<TResult, TKey>(
        Func<TSession, IAsyncEnumerable<TResult>> query,
        Func<TResult, TKey> keySelector,
        int prefetchPerShard = 1,
        CancellationToken cancellationToken = default)
        where TKey : IComparable<TKey>;

    /// <summary>
    /// Executes a globally ordered query using the EAGER strategy (materialize all items from all shards, then merge).
    /// </summary>
    /// <typeparam name="TResult">Result item type.</typeparam>
    /// <typeparam name="TKey">Ordering key type.</typeparam>
    /// <param name="query">Per-shard query delegate producing an async stream.</param>
    /// <param name="keySelector">Key selector used for ordering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Globally ordered shard items (eager materialization).</returns>
    IAsyncEnumerable<ShardItem<TResult>> QueryAllShardsOrderedEagerAsync<TResult, TKey>(
        Func<TSession, IAsyncEnumerable<TResult>> query,
        Func<TResult, TKey> keySelector,
        CancellationToken cancellationToken = default)
        where TKey : IComparable<TKey>;

    /// <summary>
    /// Executes a query on all shards and projects the results using a selector function.
    /// </summary>
    /// <typeparam name="TResult">The type of the query result.</typeparam>
    /// <typeparam name="TProjected">The type of the projected result.</typeparam>
    /// <param name="query">A function that defines the query to execute on each shard session.</param>
    /// <param name="selector">A function to project the query results.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An asynchronous stream containing the projected results from all shards.</returns>
    IAsyncEnumerable<TProjected> QueryAndProjectAsync<TResult, TProjected>(
        Func<TSession, IAsyncEnumerable<TResult>> query,
        Func<TResult, TProjected> selector,
        CancellationToken cancellationToken = default);
}