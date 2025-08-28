using System.Runtime.CompilerServices;

namespace Shardis.Query.Internals;

/// <summary>
/// Deterministic ordered k-way merge for already locally ordered shard streams.
/// Performs a streaming multi-way merge without materializing full result sets.
/// </summary>
internal static class OrderedMerge
{
    public static async IAsyncEnumerable<T> Merge<T, TKey>(IEnumerable<IAsyncEnumerable<T>> sources, Func<T, TKey> keySelector, [EnumeratorCancellation] CancellationToken ct = default)
        where TKey : IComparable<TKey>
    {
        var enumerators = new List<EnumeratorState<T, TKey>>();
        var index = 0;
        foreach (var src in sources)
        {
            var e = src.GetAsyncEnumerator(ct);
            if (await e.MoveNextAsync())
            {
                var current = e.Current;
                enumerators.Add(new EnumeratorState<T, TKey>(index++, e, current, keySelector(current)));
            }
            else
            {
                await e.DisposeAsync();
            }
        }

        var heap = new MinHeap<EnumeratorState<T, TKey>>(enumerators, EnumeratorState<T, TKey>.Comparer.Instance);

        while (heap.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var smallest = heap.Pop();
            yield return smallest.Current;
            if (await smallest.Enumerator.MoveNextAsync())
            {
                smallest.Current = smallest.Enumerator.Current;
                smallest.Key = keySelector(smallest.Current);
                heap.Push(smallest);
            }
            else
            {
                await smallest.Enumerator.DisposeAsync();
            }
        }
    }

    private sealed class EnumeratorState<T, TKey>(int order, IAsyncEnumerator<T> enumerator, T current, TKey key)
        where TKey : IComparable<TKey>
    {
        public int Order { get; } = order;
        public IAsyncEnumerator<T> Enumerator { get; } = enumerator;
        public T Current { get; set; } = current;
        public TKey Key { get; set; } = key;

        public sealed class Comparer : IComparer<EnumeratorState<T, TKey>>
        {
            public static readonly Comparer Instance = new();
            public int Compare(EnumeratorState<T, TKey>? x, EnumeratorState<T, TKey>? y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x is null) return -1;
                if (y is null) return 1;
                var keyComp = x.Key.CompareTo(y.Key);
                return keyComp != 0 ? keyComp : x.Order.CompareTo(y.Order);
            }
        }
    }

    private sealed class MinHeap<T>
    {
        private readonly List<T> _data;
        private readonly IComparer<T> _cmp;

        public int Count => _data.Count;

        public MinHeap(IEnumerable<T> init, IComparer<T> cmp)
        {
            _data = new List<T>(init);
            _cmp = cmp;
            for (int i = Parent(_data.Count - 1); i >= 0; i--)
            {
                HeapifyDown(i);
            }
        }

        public void Push(T item)
        {
            _data.Add(item);
            HeapifyUp(_data.Count - 1);
        }

        public T Pop()
        {
            var root = _data[0];
            var last = _data[^1];
            _data.RemoveAt(_data.Count - 1);
            if (_data.Count > 0)
            {
                _data[0] = last;
                HeapifyDown(0);
            }
            return root;
        }

        private void HeapifyUp(int i)
        {
            while (i > 0)
            {
                var p = Parent(i);
                if (_cmp.Compare(_data[i], _data[p]) >= 0) break;
                (_data[i], _data[p]) = (_data[p], _data[i]);
                i = p;
            }
        }

        private void HeapifyDown(int i)
        {
            while (true)
            {
                var l = Left(i);
                var r = Right(i);
                var smallest = i;
                if (l < _data.Count && _cmp.Compare(_data[l], _data[smallest]) < 0) smallest = l;
                if (r < _data.Count && _cmp.Compare(_data[r], _data[smallest]) < 0) smallest = r;
                if (smallest == i) break;
                (_data[i], _data[smallest]) = (_data[smallest], _data[i]);
                i = smallest;
            }
        }

        private static int Parent(int i) => (i - 1) / 2;
        private static int Left(int i) => 2 * i + 1;
        private static int Right(int i) => 2 * i + 2;
    }
}