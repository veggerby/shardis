using System.Linq.Expressions;

namespace Shardis.Querying.Linq;

/// <summary>
/// Represents a deferred, executable query plan across shards.
/// </summary>
public sealed class ShardQueryPlan<T>
{
    internal List<Expression<Func<T, bool>>> Filters { get; } = [];
    internal LambdaExpression? OrderByExpression { get; private set; }
    internal bool OrderByDescending { get; private set; } = false;

    internal LambdaExpression? Selector { get; private set; }

    /// <summary>
    /// Adds a filter (Where clause) to the plan.
    /// </summary>
    public void AddFilter(Expression<Func<T, bool>> predicate)
    {
        Filters.Add(predicate);
    }

    /// <summary>
    /// Sets the OrderBy key expression.
    /// </summary>
    public void SetOrderBy<TKey>(Expression<Func<T, TKey>> keySelector, bool descending = false)
    {
        OrderByExpression = keySelector;
        OrderByDescending = descending;
    }

    /// <summary>
    /// Sets a Select projection.
    /// </summary>
    public void SetSelector<TResult>(Expression<Func<T, TResult>> selector)
    {
        Selector = selector;
    }

    /// <summary>
    /// Clones the plan to another compatible type (for projection support).
    /// </summary>
    public ShardQueryPlan<TResult> Transform<TResult>()
    {
        return new ShardQueryPlan<TResult>
        {
            // Transformations are limited — this assumes a new pipeline begins from Select
            // You may optionally carry over filters if semantics allow
        };
    }
}