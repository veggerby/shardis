using System.Linq.Expressions;

namespace Shardis.Querying.Linq;

/// <summary>
/// Minimal placeholder query provider. Enumeration and execution are not implemented yet.
/// </summary>
public class ShardProvider : IQueryProvider
{
    /// <summary>
    /// Creates a non-generic query for the supplied expression.
    /// </summary>
    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments()[0];
        return (IQueryable)Activator.CreateInstance(typeof(ShardQueryable<>).MakeGenericType(elementType), this, expression)!;
    }

    /// <summary>
    /// Creates a strongly typed query for the supplied expression.
    /// </summary>
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new ShardQueryable<TElement>(this, expression);
    }

    /// <summary>
    /// Not implemented synchronous execution.
    /// </summary>
    public object Execute(Expression expression)
    {
        // Synchronous version (not useful here)
        throw new NotImplementedException();
    }

    /// <summary>
    /// Not implemented synchronous execution.
    /// </summary>
    public TResult Execute<TResult>(Expression expression)
    {
        // Synchronous version (not useful here)
        throw new NotImplementedException();
    }
}