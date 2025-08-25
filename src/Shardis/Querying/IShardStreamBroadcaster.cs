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
    /// Executes a globally ordered query using a LINQ ordering expression and backend-native execution.
    /// </summary>
    /// <typeparam name="TResult">The type of the query result.</typeparam>
    /// <typeparam name="TKey">The type of the key used for ordering.</typeparam>
    /// <param name="query">A function that defines the query to execute on each shard session.</param>
    /// <param name="keySelector">A function to extract the key for ordering.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An asynchronous stream containing the globally ordered results from all shards.</returns>
    IAsyncEnumerable<ShardItem<TResult>> QueryAllShardsOrderedAsync<TResult, TKey>(
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