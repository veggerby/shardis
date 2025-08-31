using System.Linq.Expressions;

using Shardis.Querying.Linq;

namespace Shardis.Model;

internal sealed class NoOpQueryExecutor<TSession> : IShardQueryExecutor<TSession>
{
    public static readonly NoOpQueryExecutor<TSession> Instance = new();
    public IAsyncEnumerable<T> Execute<T>(TSession session, Expression<Func<IQueryable<T>, IQueryable<T>>> linqExpr) where T : notnull => throw new NotSupportedException("No query executor configured for this shard.");
    public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(TSession session, Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr, Func<T, TKey> keySelector) where T : notnull => throw new NotSupportedException("No query executor configured for this shard.");
}