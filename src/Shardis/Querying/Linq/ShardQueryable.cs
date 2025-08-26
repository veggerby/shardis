using System.Collections;
using System.Linq.Expressions;

namespace Shardis.Querying.Linq;

/// <summary>
/// Minimal <see cref="IQueryable{T}"/> implementation used as a stand-in for a future shard-aware LINQ provider.
/// Enumeration is not currently supported and will throw.
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
public class ShardQueryable<T> : IQueryable<T>
{
    /// <summary>
    /// Initializes an instance with a provider and a default constant expression.
    /// </summary>
    public ShardQueryable(IQueryProvider provider)
    {
        Provider = provider;
        Expression = Expression.Constant(this);
    }

    /// <summary>
    /// Initializes an instance with a provider and explicit expression.
    /// </summary>
    public ShardQueryable(IQueryProvider provider, Expression expression)
    {
        Provider = provider;
        Expression = expression;
    }

    /// <inheritdoc />
    public Type ElementType => typeof(T);
    /// <inheritdoc />
    public Expression Expression { get; }
    /// <inheritdoc />
    public IQueryProvider Provider { get; }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}