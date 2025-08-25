using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Shardis.Model;

namespace Shardis.Querying;

public static class ShardStreamBroadcasterExtensions
{
    /// <summary>
    /// Merges an unordered stream of items by collecting and sorting in-memory.
    /// </summary>
    public static async IAsyncEnumerable<T> MergeOrdered<T, TKey>(
        this IAsyncEnumerable<T> source,
        Func<T, TKey> keySelector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TKey : IComparable<TKey>
    {
        var buffer = new List<T>();

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            buffer.Add(item);
        }

        foreach (var item in buffer.OrderBy(keySelector))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Merges multiple ordered async streams into a globally ordered output using a K-way merge.
    /// </summary>
    public static async IAsyncEnumerable<T> MergeSortedBy<T, TKey>(
        this IEnumerable<IAsyncEnumerable<T>> shardStreams,
        Func<T, TKey> keySelector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TKey : IComparable<TKey>
    {
        var enumerators = shardStreams
            .Select(stream => new ShardEnumeratorWrapper<T, TKey>(stream.GetAsyncEnumerator(cancellationToken), keySelector))
            .ToList();

        // Prime all enumerators
        await Task.WhenAll(enumerators.Select(e => e.MoveNextAsync()));

        while (true)
        {
            var candidates = enumerators.Where(e => e.HasValue).ToList();
            if (candidates.Count == 0)
            {
                yield break;
            }

            var next = candidates.Aggregate((min, cur) => cur.CurrentKey.CompareTo(min.CurrentKey) < 0 ? cur : min);
            yield return next.Current;

            await next.MoveNextAsync();
        }
    }

    /// <summary>
    /// Executes a query against all shards in parallel and returns a flattened unordered stream.
    /// </summary>
    public static async IAsyncEnumerable<ShardItem<T>> QueryMergedAsync<TSession, T>(
        this IShardStreamBroadcaster<TSession> broadcaster,
        Func<TSession, IAsyncEnumerable<T>> query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in broadcaster.QueryAllShardsAsync(query, cancellationToken))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Executes a query against all shards and returns a globally ordered merged stream.
    /// </summary>
    public static async IAsyncEnumerable<ShardItem<T>> QueryAndMergeSortedByAsync<TSession, T, TKey>(
        this IShardStreamBroadcaster<TSession> broadcaster,
        Func<TSession, IAsyncEnumerable<T>> query,
        Func<T, TKey> keySelector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TKey : IComparable<TKey>
    {
        var shardStreams = broadcaster
            .QueryAllShardsAsync(query, cancellationToken)
            .GroupByShard()
            .Select(pair =>
                new ShardisAsyncShardEnumerator<T>(
                    pair.Key,
                    pair.Value.GetAsyncEnumerator(cancellationToken)
                )
            )
            .ToList();

        await using var ordered = new ShardisAsyncOrderedEnumerator<T, TKey>(shardStreams, keySelector, cancellationToken);

        while (await ordered.MoveNextAsync())
        {
            yield return ordered.Current;
        }
    }

    private sealed class ShardEnumeratorWrapper<T, TKey>(IAsyncEnumerator<T> enumerator, Func<T, TKey> keySelector)
        where TKey : IComparable<TKey>
    {
        public IAsyncEnumerator<T> Enumerator { get; } = enumerator;
        public Func<T, TKey> KeySelector { get; } = keySelector;
        public T Current { get; private set; } = default!;
        public TKey CurrentKey => KeySelector(Current);
        public bool HasValue { get; private set; }

        public async Task<bool> MoveNextAsync()
        {
            if (await Enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                Current = Enumerator.Current;
                HasValue = true;
                return true;
            }

            HasValue = false;
            return false;
        }
    }

    /// <summary>
    /// Groups a sequence of <see cref="ShardItem{T}"/> into per-shard streams.
    /// </summary>
    public static IReadOnlyDictionary<ShardId, IAsyncEnumerable<T>> GroupByShard<T>(
        this IAsyncEnumerable<ShardItem<T>> source)
    {
        var channels = new ConcurrentDictionary<ShardId, Channel<T>>();

        async Task Pump()
        {
            await foreach (var item in source)
            {
                var channel = channels.GetOrAdd(item.ShardId, _ => Channel.CreateUnbounded<T>());
                await channel.Writer.WriteAsync(item.Item);
            }

            foreach (var ch in channels.Values)
            {
                ch.Writer.Complete();
            }
        }

        _ = Task.Run(Pump); // Fire and forget the pump task

        return channels.ToDictionary(
            kvp => kvp.Key,
            kvp => ReadAsync(kvp.Value.Reader)
        );

        static async IAsyncEnumerable<T> ReadAsync(ChannelReader<T> reader)
        {
            while (await reader.WaitToReadAsync())
            {
                while (reader.TryRead(out var item))
                {
                    yield return item;
                }
            }
        }
    }

    public static async IAsyncEnumerable<TResult> QueryAndProjectAsync<TSession, T, TResult>(
    this IShardStreamBroadcaster<TSession> broadcaster,
    Func<TSession, IAsyncEnumerable<T>> query,
    Func<T, TResult> selector,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in broadcaster.QueryAllShardsAsync(query, cancellationToken))
        {
            yield return selector(item.Item);
        }
    }

    public static IShardisAsyncEnumerator<T> ToShardisEnumerator<T>(
        this IAsyncEnumerable<T> source,
        ShardId shardId)
    {
        return new SimpleShardisAsyncEnumerator<T>(shardId, source);
    }
}