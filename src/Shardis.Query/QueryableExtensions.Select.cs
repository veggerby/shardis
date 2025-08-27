using System.Linq.Expressions;

namespace Shardis.Query;

/// <summary>Projection extension (Select stage).</summary>
/// <summary>Select stage composition extension for shard queries.</summary>
public static class ShardQueryableSelectExtensions
{
    /// <summary>Apply a projection to the query model (at most one projection supported in MVP).</summary>
    public static IShardQueryable<TResult> Select<T, TResult>(this IShardQueryable<T> source,
                                                              Expression<Func<T, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        var model = source.Model.WithSelect(selector);
        return new ShardQueryable<TResult>(source.Executor, model);
    }
}