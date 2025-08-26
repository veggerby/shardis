namespace Shardis.Querying;

/// <summary>
/// Defines the contract for broadcasting queries to all shards and aggregating results.
/// </summary>
/// <typeparam name="TSession">The type of session used for querying shards.</typeparam>
public interface IShardBroadcaster<TSession>
{
    /// <summary>
    /// Executes a query on all shards in parallel and aggregates the results.
    /// </summary>
    /// <typeparam name="TResult">The type of the query result.</typeparam>
    /// <param name="query">A function that defines the query to execute on each shard session.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, containing the aggregated results from all shards.</returns>
    Task<IEnumerable<TResult>> QueryAllShardsAsync<TResult>(Func<TSession, Task<IEnumerable<TResult>>> query, CancellationToken cancellationToken = default);
}