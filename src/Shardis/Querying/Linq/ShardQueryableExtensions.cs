using System.Linq.Expressions;

namespace Shardis.Querying.Linq;

/// <summary>
/// Placeholder extension methods defining the fluent surface for a future shard-aware LINQ provider.
/// Current implementations are no-ops returning the source to allow compilation of higher layers.
/// </summary>
public static class ShardQueryableExtensions
{
    /// <summary>
    /// Filters a sharded queryable sequence based on a predicate expression.
    /// </summary>
    public static IShardQueryable<T> Where<T>(this IShardQueryable<T> source, Expression<Func<T, bool>> predicate) => source;

    /// <summary>
    /// Orders a sharded queryable sequence by the specified key selector.
    /// </summary>
    public static IShardQueryable<T> OrderBy<T, TKey>(this IShardQueryable<T> source, Expression<Func<T, TKey>> keySelector) where TKey : IComparable<TKey> => source;

    /// <summary>
    /// Performs a subsequent ordering of a sharded queryable sequence.
    /// </summary>
    public static IShardQueryable<T> ThenBy<T, TKey>(this IShardQueryable<T> source, Expression<Func<T, TKey>> keySelector) where TKey : IComparable<TKey> => source;
}