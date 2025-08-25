using System.Collections;
using System.Linq.Expressions;

namespace Shardis.Querying.Linq;

public class ShardQueryable<T> : IQueryable<T>
{
    public ShardQueryable(IQueryProvider provider)
    {
        Provider = provider;
        Expression = Expression.Constant(this);
    }

    public ShardQueryable(IQueryProvider provider, Expression expression)
    {
        Provider = provider;
        Expression = expression;
    }

    public Type ElementType => typeof(T);
    public Expression Expression { get; }
    public IQueryProvider Provider { get; }

    public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}