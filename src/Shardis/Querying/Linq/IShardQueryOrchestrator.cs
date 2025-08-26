namespace Shardis.Querying.Linq;

/// <summary>
/// Coordinates fan-out execution of a logical query plan across shards and materializes or streams results.
/// </summary>
public interface IShardQueryOrchestrator
{
    /// <summary>
    /// Executes the supplied <paramref name="plan"/> and materializes results into a list.
    /// </summary>
    /// <typeparam name="T">Result element type.</typeparam>
    /// <param name="plan">The compiled shard query plan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All results aggregated into memory.</returns>
    Task<List<T>> ExecuteToListAsync<T>(ShardQueryPlan<T> plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query plan producing a streaming asynchronous sequence (preferred for large result sets).
    /// </summary>
    /// <typeparam name="T">Result element type.</typeparam>
    /// <param name="plan">The compiled shard query plan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async stream of aggregated results.</returns>
    IAsyncEnumerable<T> ExecuteAsyncEnumerable<T>(ShardQueryPlan<T> plan, CancellationToken cancellationToken = default);
}