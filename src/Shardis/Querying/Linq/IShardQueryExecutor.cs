using System.Linq.Expressions;

namespace Shardis.Querying.Linq;

/// <summary>
/// Executes provider-specific LINQ expressions against a shard session producing asynchronous result streams.
/// Implementations translate high-level expressions into backend queries (e.g., SQL, document queries) and must
/// stream results without buffering the full sequence unless required by terminal operations.
/// </summary>
/// <typeparam name="TSession">The session type for the shard backend.</typeparam>
public interface IShardQueryExecutor<TSession>
{
    /// <summary>
    /// Executes a LINQ pipeline returning an unordered asynchronous stream of results.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="session">The shard session to execute against.</param>
    /// <param name="linqExpr">Expression describing the query (from IQueryable to transformed IQueryable).</param>
    /// <returns>An asynchronous stream of query results.</returns>
    IAsyncEnumerable<T> Execute<T>(TSession session, Expression<Func<IQueryable<T>, IQueryable<T>>> linqExpr) where T : notnull;

    /// <summary>
    /// Executes an ordered LINQ pipeline returning a locally ordered asynchronous stream of results.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <typeparam name="TKey">Ordering key type.</typeparam>
    /// <param name="session">The shard session.</param>
    /// <param name="orderedExpr">Expression producing an ordered queryable.</param>
    /// <param name="keySelector">Delegate extracting the ordering key (used for global merge).</param>
    /// <returns>An asynchronous stream ordered within this shard.</returns>
    IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(
        TSession session,
        Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr,
        Func<T, TKey> keySelector) where T : notnull;
}