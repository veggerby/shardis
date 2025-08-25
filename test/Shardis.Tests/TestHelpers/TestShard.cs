using System.Linq.Expressions;

using Shardis.Model;
using Shardis.Querying.Linq;

namespace Shardis.Tests.TestHelpers;

public class TestShard<TSession>(string id, TSession session) : IShard<TSession>
{
    public ShardId ShardId { get; } = new ShardId(id);
    private readonly TSession _session = session;
    public IShardQueryExecutor<TSession> QueryExecutor { get; } = new NoOpQueryExecutor();

    public TSession CreateSession() => _session;

    private sealed class NoOpQueryExecutor : IShardQueryExecutor<TSession>
    {
        public IAsyncEnumerable<T> Execute<T>(TSession session, Expression<Func<IQueryable<T>, IQueryable<T>>> linqExpr) where T : notnull
            => throw new NotSupportedException();
        public IAsyncEnumerable<T> ExecuteOrdered<T, TKey>(TSession session, Expression<Func<IQueryable<T>, IOrderedQueryable<T>>> orderedExpr, Func<T, TKey> keySelector) where T : notnull
            => throw new NotSupportedException();
    }
}