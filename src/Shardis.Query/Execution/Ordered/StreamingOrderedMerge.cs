using System.Runtime.CompilerServices;

namespace Shardis.Query.Execution.Ordered;

/// <summary>
/// K-way streaming merge over already-ordered per-shard sequences. Stable across shards by shard index to preserve intra-key grouping order.
/// </summary>
internal static class StreamingOrderedMerge
{
    public static async IAsyncEnumerable<T> Merge<T, TKey>(IReadOnlyList<IAsyncEnumerable<T>> sources,
                                                           Func<T, TKey> keySelector,
                                                           IComparer<TKey> comparer,
                                                           bool descending,
                                                           [EnumeratorCancellation] CancellationToken ct)
    {
        if (sources.Count == 0) yield break;

        var enumerators = new List<IAsyncEnumerator<T>>(sources.Count);
        try
        {
            for (var i = 0; i < sources.Count; i++)
            {
                enumerators.Add(sources[i].GetAsyncEnumerator(ct));
            }

            // Concurrently fetch first element from every shard to avoid N serial latency accrual.
            var firstFetchTasks = new Task<InitialItem<T, TKey>>[enumerators.Count];
            for (var i = 0; i < enumerators.Count; i++)
            {
                var index = i;
                var e = enumerators[index];
                firstFetchTasks[index] = FetchFirstAsync(e, index, keySelector, ct);
            }

            await Task.WhenAll(firstFetchTasks).ConfigureAwait(false);

            var heap = new List<HeapItem<T, TKey>>(enumerators.Count);
            for (var i = 0; i < firstFetchTasks.Length; i++)
            {
                var r = firstFetchTasks[i].Result; // already completed
                if (r.HasValue)
                {
                    heap.Add(new HeapItem<T, TKey>(r.Value, r.Key, r.ShardIndex));
                }
            }
            if (heap.Count == 0) yield break;
            BuildHeap(heap, comparer, descending);

            while (heap.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var top = heap[0];
                yield return top.Value;
                var e = enumerators[top.ShardIndex];
                if (await e.MoveNextAsync().ConfigureAwait(false))
                {
                    var v = e.Current;
                    heap[0] = new HeapItem<T, TKey>(v, keySelector(v)!, top.ShardIndex);
                }
                else
                {
                    // remove root
                    var last = heap[^1];
                    heap.RemoveAt(heap.Count - 1);
                    if (heap.Count == 0) break;
                    heap[0] = last;
                }
                HeapifyDown(heap, 0, comparer, descending);
            }
        }
        finally
        {
            foreach (var e in enumerators)
            {
                try { if (e is not null) await e.DisposeAsync().ConfigureAwait(false); } catch { /* swallow */ }
            }
        }
    }

    private static void BuildHeap<T, TKey>(List<HeapItem<T, TKey>> heap, IComparer<TKey> comparer, bool descending)
    {
        for (int i = heap.Count / 2 - 1; i >= 0; i--)
        {
            HeapifyDown(heap, i, comparer, descending);
        }
    }

    private static void HeapifyDown<T, TKey>(List<HeapItem<T, TKey>> heap, int index, IComparer<TKey> comparer, bool descending)
    {
        while (true)
        {
            int left = index * 2 + 1;
            if (left >= heap.Count) return;
            int right = left + 1;
            int best = left;
            if (right < heap.Count && Compare(heap[right], heap[left], comparer, descending) < 0) best = right;
            if (Compare(heap[best], heap[index], comparer, descending) < 0)
            {
                (heap[index], heap[best]) = (heap[best], heap[index]);
                index = best;
            }
            else return;
        }
    }

    private static int Compare<T, TKey>(HeapItem<T, TKey> a, HeapItem<T, TKey> b, IComparer<TKey> comparer, bool descending)
    {
        int c = comparer.Compare(a.Key, b.Key);
        if (c == 0)
        {
            // stable tie-break by shard index
            c = a.ShardIndex.CompareTo(b.ShardIndex);
        }
        return descending ? -c : c;
    }

    private readonly record struct HeapItem<T, TKey>(T Value, TKey Key, int ShardIndex);

    private static async Task<InitialItem<T, TKey>> FetchFirstAsync<T, TKey>(IAsyncEnumerator<T> enumerator,
                                                                             int shardIndex,
                                                                             Func<T, TKey> keySelector,
                                                                             CancellationToken ct)
    {
        if (await enumerator.MoveNextAsync().ConfigureAwait(false))
        {
            var current = enumerator.Current;
            return new InitialItem<T, TKey>(true, current!, keySelector(current)!, shardIndex);
        }
        return new InitialItem<T, TKey>(false, default!, default!, shardIndex);
    }

    private readonly record struct InitialItem<T, TKey>(bool HasValue, T Value, TKey Key, int ShardIndex);
}