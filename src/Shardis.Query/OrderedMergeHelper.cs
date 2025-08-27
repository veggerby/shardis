namespace Shardis.Query;

/// <summary>Public ordered merge helper for globally ordering locally ordered shard streams.</summary>
public static class OrderedMergeHelper
{
    /// <summary>
    /// Merge ordered shard streams into a globally ordered sequence using the provided key selector.
    /// </summary>
    public static IAsyncEnumerable<T> Merge<T, TKey>(IEnumerable<IAsyncEnumerable<T>> sources, Func<T, TKey> keySelector, CancellationToken ct = default)
        where TKey : IComparable<TKey>
        => Internals.OrderedMerge.Merge(sources, keySelector, ct);
}