using System.Linq.Expressions;

namespace Shardis.Querying.Linq;

public static class ShardQueryableExtensions
{
    // Placeholder extension points – detailed LINQ provider infrastructure removed for now.
    public static IShardQueryable<T> Where<T>(this IShardQueryable<T> source, Expression<Func<T, bool>> predicate) => source;
    public static IShardQueryable<T> OrderBy<T, TKey>(this IShardQueryable<T> source, Expression<Func<T, TKey>> keySelector) where TKey : IComparable<TKey> => source;
    public static IShardQueryable<T> ThenBy<T, TKey>(this IShardQueryable<T> source, Expression<Func<T, TKey>> keySelector) where TKey : IComparable<TKey> => source;
}