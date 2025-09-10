using System.Collections.Generic;

using Shardis.Query.Execution.Ordered;

namespace Shardis.Query;

/// <summary>
/// Helpers for ordered (streaming) k-way merge over already sorted per-shard sequences.
/// </summary>
public static class OrderedMergeHelper
{
    /// <summary>
    /// Perform a streaming k-way merge across pre-ordered shard sequences using <paramref name="keySelector"/>.
    /// Stable within equal keys (ties broken by shard index order). Each input must already be sorted.
    /// </summary>
    public static IAsyncEnumerable<T> MergeStreaming<T, TKey>(IReadOnlyList<IAsyncEnumerable<T>> sources,
                                                              Func<T, TKey> keySelector,
                                                              IComparer<TKey>? comparer = null,
                                                              bool descending = false,
                                                              CancellationToken ct = default)
    {
        comparer ??= Comparer<TKey>.Default;
        return StreamingOrderedMerge.Merge(sources, keySelector, comparer, descending, ct);
    }

    /// <summary>
    /// Backwards-compatible alias for ordered merge (ascending) using default comparer.
    /// </summary>
    public static IAsyncEnumerable<T> Merge<T, TKey>(IEnumerable<IAsyncEnumerable<T>> sources,
                                                     Func<T, TKey> keySelector,
                                                     CancellationToken ct = default)
    {
        var list = sources as IReadOnlyList<IAsyncEnumerable<T>> ?? sources.ToList();
        return MergeStreaming(list, keySelector, comparer: null, descending: false, ct);
    }
}