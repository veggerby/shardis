using System.Linq.Expressions;
using Shardis.Query.Execution;

namespace Shardis.Query;

/// <summary>
/// Ergonomic extensions for starting shard queries without explicitly calling <c>ShardQuery.For&lt;T&gt;</c>.
/// </summary>
public static class ShardQueryExecutorExtensions
{
    /// <summary>
    /// Begin a shard-wide query for <typeparamref name="T"/>. Shorthand for <c>ShardQuery.For&lt;T&gt;(executor)</c>.
    /// </summary>
    public static IShardQueryable<T> Query<T>(this IShardQueryExecutor executor)
        => ShardQuery.For<T>(executor);

    /// <summary>
    /// Begin a shard-wide query for <typeparamref name="T"/> applying optional <paramref name="where"/> and <paramref name="select"/> stages.
    /// When <typeparamref name="TResult"/> differs from <typeparamref name="T"/>, a <paramref name="select"/> expression must be provided.
    /// </summary>
    public static IShardQueryable<TResult> Query<T, TResult>(this IShardQueryExecutor executor,
                                                             Expression<Func<T, bool>>? where = null,
                                                             Expression<Func<T, TResult>>? select = null)
    {
        var root = ShardQuery.For<T>(executor);

        if (where is not null)
        {
            root = ShardQueryableExtensions.Where(root, where);
        }

        if (select is not null)
        {
            return ShardQueryableSelectExtensions.Select((IShardQueryable<T>)root, select);
        }

        if (typeof(TResult) == typeof(T))
        {
            return (IShardQueryable<TResult>)root;
        }

        throw new InvalidOperationException("Projection (select) must be supplied when the result type differs.");
    }
}
