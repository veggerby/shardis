using System.Linq.Expressions;

using Marten;

using Shardis.Querying.Linq;

namespace Shardis.Marten;

public sealed class MartenQueryExecutor : IShardQueryExecutor<IDocumentSession>
{
    public static readonly MartenQueryExecutor Instance = new();

    public IAsyncEnumerable<T> Execute<T>(IDocumentSession session, Expression<Func<IQueryable<T>, IQueryable<T>>> expr) where T : notnull
    {
        var queryable = session.Query<T>();
        var transformed = expr.Compile().Invoke(queryable);
        return Enumerate(transformed);
    }

    public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(
        IDocumentSession session,
        Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr,
        Func<T, TKey> keySelector) where T : notnull
    {
        var queryable = session.Query<T>();
        var transformed = orderedExpr.Compile().Invoke(queryable);
        return Enumerate(transformed); // Preserves backend ordering
    }

    private static async IAsyncEnumerable<TItem> Enumerate<TItem>(IEnumerable<TItem> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.Yield();
        }
    }
}