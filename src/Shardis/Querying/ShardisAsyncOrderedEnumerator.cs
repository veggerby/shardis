namespace Shardis.Querying;

/// <summary>
/// A globally ordered enumerator that performs a k-way merge across multiple ordered shard streams.
/// </summary>
internal sealed class ShardisAsyncOrderedEnumerator<T, TKey> : IShardisAsyncOrderedEnumerator<T>
    where TKey : IComparable<TKey>
{
    private readonly IReadOnlyList<IShardisAsyncEnumerator<T>> _streams;
    private readonly Func<T, TKey> _keySelector;
    private readonly int _prefetchPerShard;
    private readonly CancellationToken _cancellationToken;
    private readonly IOrderedMergeProbe? _probe;

    private readonly List<StreamState> _states;
    private readonly PriorityQueue<HeapItem, HeapItem> _heap;
    private ShardItem<T>? _current;

    private sealed record StreamState(int ShardIndex, IShardisAsyncEnumerator<T> Enumerator, long Sequence, int Buffered, bool Completed);
    private readonly struct HeapItem(TKey key, int shardIndex, long sequence, ShardItem<T> item) : IComparable<HeapItem>
    {
        public readonly TKey Key = key;
        public readonly int ShardIndex = shardIndex;
        public readonly long Sequence = sequence;
        public readonly ShardItem<T> Item = item;

        public int CompareTo(HeapItem other)
        {
            var c = Key.CompareTo(other.Key);
            if (c != 0)
            {
                return c;
            }

            c = ShardIndex.CompareTo(other.ShardIndex);
            if (c != 0)
            {
                return c;
            }

            return Sequence.CompareTo(other.Sequence);
        }
    }

    public ShardisAsyncOrderedEnumerator(IEnumerable<IShardisAsyncEnumerator<T>> shardStreams, Func<T, TKey> keySelector, int prefetchPerShard, CancellationToken cancellationToken = default, IOrderedMergeProbe? probe = null)
    {
        ArgumentNullException.ThrowIfNull(shardStreams);
        ArgumentNullException.ThrowIfNull(keySelector);

        if (prefetchPerShard < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(prefetchPerShard));
        }

        _streams = shardStreams.ToList();
        _keySelector = keySelector;
        _prefetchPerShard = prefetchPerShard;
        _cancellationToken = cancellationToken;
        _probe = probe;
        _states = new List<StreamState>(_streams.Count);

        for (int i = 0; i < _streams.Count; i++)
        {
            _states.Add(new StreamState(i, _streams[i], 0, 0, false));
        }

        _heap = new PriorityQueue<HeapItem, HeapItem>();
    }

    public int ShardCount => _streams.Count;
    public bool IsComplete { get; private set; }
    public bool HasValue => _current is not null;
    public bool IsPrimed { get; private set; }
    public ShardItem<T> Current => _current ?? throw new ShardisException("Enumeration has not started.");

    public async ValueTask<bool> MoveNextAsync()
    {
        _cancellationToken.ThrowIfCancellationRequested();

        if (!IsPrimed)
        {
            // Prime each shard up to limit
            for (int i = 0; i < _states.Count; i++)
            {
                await TopUpAsync(i).ConfigureAwait(false);
            }

            IsPrimed = true;
        }

        if (_heap.TryDequeue(out var item, out _))
        {
            _current = item.Item;
            var state = _states[item.ShardIndex];

            if (!state.Completed)
            {
                // Decrement buffered; then attempt top-up for that shard
                ReplaceState(item.ShardIndex, state with { Buffered = Math.Max(0, state.Buffered - 1) });
                await TopUpAsync(item.ShardIndex).ConfigureAwait(false);
            }

            return true;
        }

        // Heap empty: either complete or waiting (but no buffered items) => treat as completion if all shards done
        foreach (var s in _states.Where(s => !s.Completed))
        {
            await TopUpAsync(s.ShardIndex).ConfigureAwait(false);
        }

        if (_heap.TryDequeue(out var lateItem, out _))
        {
            _current = lateItem.Item;
            var st = _states[lateItem.ShardIndex];
            ReplaceState(lateItem.ShardIndex, st with { Buffered = Math.Max(0, st.Buffered - 1) });
            await TopUpAsync(lateItem.ShardIndex).ConfigureAwait(false);

            return true;
        }

        IsComplete = _states.All(s => s.Completed);
        _current = null;

        return false;
    }

    private async ValueTask TopUpAsync(int shardIndex)
    {
        var state = _states[shardIndex];
        if (state.Completed) return;

        while (state.Buffered < _prefetchPerShard)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            bool advanced;

            try
            {
                advanced = await state.Enumerator.MoveNextAsync().ConfigureAwait(false);
            }
            catch
            {
                await DisposeAsync().ConfigureAwait(false);
                throw;
            }

            if (!advanced)
            {
                ReplaceState(shardIndex, state with { Completed = true });
                break;
            }

            var shardItem = state.Enumerator.Current;
            var key = _keySelector(shardItem.Item);
            var newSequence = state.Sequence + 1;
            var heapItem = new HeapItem(key, shardIndex, newSequence, shardItem);

            _heap.Enqueue(heapItem, heapItem);
            state = state with { Sequence = newSequence, Buffered = state.Buffered + 1 };
            ReplaceState(shardIndex, state);
            _probe?.OnHeapSize(_heap.Count);
        }
    }

    private void ReplaceState(int index, StreamState newState)
    {
        _states[index] = newState;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var s in _states)
        {
            await s.Enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Internal probe hook for tests to observe merge characteristics.
/// </summary>
internal interface IOrderedMergeProbe
{
    void OnHeapSize(int size);
}