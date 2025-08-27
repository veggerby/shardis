using System.Linq.Expressions;

namespace Shardis.Query;

/// <summary>Guard extensions clarifying that ordering is not part of the MVP surface.</summary>
/// <summary>Extensions that deliberately throw to signal unsupported ordering in MVP.</summary>
public static class ShardQueryableOrderingGuards
{
    static NotSupportedException OrderByNotSupported()
        => new NotSupportedException(
            "OrderBy/ThenBy are not supported in the LINQ MVP. Use ShardStreamBroadcaster.QueryAllShardsOrderedStreamingAsync(keySelector) or perform ordering after materialization.");

    /// <summary>Ordering not supported; use ordered streaming broadcaster or apply after materialization.</summary>
    public static IShardQueryable<T> OrderBy<T, TKey>(this IShardQueryable<T> source, Expression<Func<T, TKey>> _) => throw OrderByNotSupported();
    /// <inheritdoc cref="OrderBy" />
    public static IShardQueryable<T> OrderByDescending<T, TKey>(this IShardQueryable<T> source, Expression<Func<T, TKey>> _) => throw OrderByNotSupported();
    /// <inheritdoc cref="OrderBy" />
    public static IShardQueryable<T> ThenBy<T, TKey>(this IShardQueryable<T> source, Expression<Func<T, TKey>> _) => throw OrderByNotSupported();
    /// <inheritdoc cref="OrderBy" />
    public static IShardQueryable<T> ThenByDescending<T, TKey>(this IShardQueryable<T> source, Expression<Func<T, TKey>> _) => throw OrderByNotSupported();
}