namespace Shardis.Routing;

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
    IAsyncEnumerable<TResult> QueryAllShardsAsync<TResult>(Func<TSession, IAsyncEnumerable<TResult>> query, CancellationToken cancellationToken = default);
}