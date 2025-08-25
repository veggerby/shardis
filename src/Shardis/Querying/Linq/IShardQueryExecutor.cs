using System.Linq.Expressions;

namespace Shardis.Querying.Linq;

public interface IShardQueryExecutor<TSession>
{
    IAsyncEnumerable<T> Execute<T>(TSession session, Expression<Func<IQueryable<T>, IQueryable<T>>> linqExpr);

    IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(
        TSession session,
        Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr,
        Func<T, TKey> keySelector);
}