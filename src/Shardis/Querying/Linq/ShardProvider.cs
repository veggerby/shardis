using System.Linq.Expressions;

namespace Shardis.Querying.Linq;

public class ShardProvider : IQueryProvider
{
    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments()[0];
        return (IQueryable)Activator.CreateInstance(typeof(ShardQueryable<>).MakeGenericType(elementType), this, expression)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new ShardQueryable<TElement>(this, expression);
    }

    public object Execute(Expression expression)
    {
        // Synchronous version (not useful here)
        throw new NotImplementedException();
    }

    public TResult Execute<TResult>(Expression expression)
    {
        // Synchronous version (not useful here)
        throw new NotImplementedException();
    }
}