using System.Linq.Expressions;

namespace Shardis.Query;

/// <summary>Composition extensions (Where stage).</summary>
/// <summary>Where stage composition extension for shard queries.</summary>
public static class ShardQueryableExtensions
{
    /// <summary>Add a predicate filter (per-shard) to the query model.</summary>
    public static IShardQueryable<T> Where<T>(this IShardQueryable<T> source,
                                              Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var model = source.Model.WithWhere(predicate);
        return new ShardQueryable<T>(source.Executor, model);
    }
}