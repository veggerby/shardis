using System.Linq.Expressions;

using Marten;

using Shardis.Querying.Linq;

namespace Shardis.Marten;

/// <summary>
/// Marten implementation of <see cref="IShardQueryExecutor{TSession}"/> translating expression trees to Marten LINQ queries.
/// </summary>
public sealed class MartenQueryExecutor : IShardQueryExecutor<IDocumentSession>
{
    /// <summary>
    /// Singleton instance to avoid repeated allocations.
    /// </summary>
    public static readonly MartenQueryExecutor Instance = new();

    /// <summary>
    /// Executes an unordered Marten LINQ query.
    /// </summary>
    public IAsyncEnumerable<T> Execute<T>(IDocumentSession session, Expression<Func<IQueryable<T>, IQueryable<T>>> expr) where T : notnull
    {
        var queryable = session.Query<T>();
        var transformed = expr.Compile().Invoke(queryable);
        return Enumerate(transformed);
    }

    /// <summary>
    /// Executes an ordered Marten LINQ query preserving backend ordering.
    /// </summary>
    public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(
        IDocumentSession session,
        Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr,
        Func<T, TKey> keySelector) where T : notnull
    {
        var queryable = session.Query<T>();
        var transformed = orderedExpr.Compile().Invoke(queryable);
        return Enumerate(transformed); // Preserves backend ordering
    }

    /// <summary>
    /// Enumerates an <see cref="IEnumerable{T}"/> as an <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    private static async IAsyncEnumerable<TItem> Enumerate<TItem>(IEnumerable<TItem> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.Yield();
        }
    }
}