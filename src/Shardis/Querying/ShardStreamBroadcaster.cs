using System.Linq.Expressions;
using System.Threading.Channels;

using Shardis.Model;

namespace Shardis.Querying;

/// <summary>
/// Provides an implementation of <see cref="IShardStreamBroadcaster{TSession}"/> streaming results from all shards in parallel.
/// </summary>
/// <typeparam name="TShard">Concrete shard type.</typeparam>
/// <typeparam name="TSession">The type of session used for querying shards.</typeparam>
public partial class ShardStreamBroadcaster<TShard, TSession> : IShardStreamBroadcaster<TSession> where TShard : IShard<TSession>
{
    private readonly IEnumerable<TShard> _shards;
    private readonly int? _channelCapacity;
    internal IMergeObserver Observer { get; }
    private readonly int _heapSampleEvery;

    /// <summary>
    /// Creates a new broadcaster.
    /// </summary>
    /// <param name="shards">Shard collection to query.</param>
    /// <param name="channelCapacity">Optional bounded channel capacity (null = unbounded).</param>
    /// <param name="observer">Optional merge observer for instrumentation (null = no-op).</param>
    /// <param name="heapSampleEvery">Heap sampling frequency for ordered merge (1 = every insertion, N = every Nth insertion).</param>
    public ShardStreamBroadcaster(IEnumerable<TShard> shards, int? channelCapacity = null, IMergeObserver? observer = null, int heapSampleEvery = 1)
    {
        ArgumentNullException.ThrowIfNull(shards, nameof(shards));
        if (!shards.Any())
        {
            throw new ArgumentException("Shard collection must not be empty", nameof(shards));
        }
        if (channelCapacity.HasValue && channelCapacity.Value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(channelCapacity), "Channel capacity must be >= 1 if specified.");
        }
        if (heapSampleEvery < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(heapSampleEvery), "heapSampleEvery must be >= 1");
        }

        _shards = shards;
        _channelCapacity = channelCapacity;
        Observer = observer ?? NoOpMergeObserver.Instance;
        _heapSampleEvery = heapSampleEvery;
    }

    /// <summary>
    /// Overload retaining previous signature (no observer / sampling) for source compatibility.
    /// </summary>
    public ShardStreamBroadcaster(IEnumerable<TShard> shards) : this(shards, null, null, 1) { }

    /// <summary>
    /// Executes an asynchronous query against all shards, streaming back <see cref="ShardItem{TItem}"/> values as they arrive.
    /// </summary>
    public IAsyncEnumerable<ShardItem<TResult>> QueryAllShardsAsync<TResult>(
        Func<TSession, IAsyncEnumerable<TResult>> query,
        CancellationToken cancellationToken = default)
    {
        return Execute();

        async IAsyncEnumerable<ShardItem<TResult>> Execute()
        {
            ArgumentNullException.ThrowIfNull(query, nameof(query));

            var channel = _channelCapacity.HasValue
                ? Channel.CreateBounded<ShardItem<TResult>>(new BoundedChannelOptions(_channelCapacity.Value)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait
                })
                : Channel.CreateUnbounded<ShardItem<TResult>>();

            var shardTasks = _shards.Select(shard => Task.Run(async () =>
            {
                var session = shard.CreateSession();
                ShardStopReason reason = ShardStopReason.Completed;
                try
                {
                    await foreach (var item in query(session).WithCancellation(cancellationToken).ConfigureAwait(false))
                    {
                        if (_channelCapacity.HasValue)
                        {
                            var write = channel.Writer.WriteAsync(new(shard.ShardId, item), cancellationToken);
                            if (!write.IsCompletedSuccessfully)
                            {
                                TryObserver(o => o.OnBackpressureWaitStart());
                                await write.ConfigureAwait(false);
                                TryObserver(o => o.OnBackpressureWaitStop());
                            }
                            else { /* completed synchronously */ }
                        }
                        else
                        {
                            await channel.Writer.WriteAsync(new(shard.ShardId, item), cancellationToken).ConfigureAwait(false);
                        }
                    }
                    TryObserver(o => o.OnShardCompleted(shard.ShardId));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    reason = ShardStopReason.Canceled;
                }
                catch (Exception)
                {
                    reason = ShardStopReason.Faulted;
                    throw;
                }
                finally
                {
                    TryObserver(o => o.OnShardStopped(shard.ShardId, reason));
                }
            }, cancellationToken)).ToList();

            var completion = Task.WhenAll(shardTasks);
            _ = completion.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    channel.Writer.TryComplete(t.Exception);
                }
                else if (t.IsCanceled)
                {
                    channel.Writer.TryComplete(new OperationCanceledException(cancellationToken));
                }
                else
                {
                    channel.Writer.TryComplete();
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                TryObserver(o => o.OnItemYielded(item.ShardId));
                yield return item;
            }

            await completion.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes an expression-based query (LINQ transformation) across all shards using backend-aware executors.
    /// </summary>
    public IAsyncEnumerable<ShardItem<TResult>> QueryAllShardsWithExpressionAsync<TResult>(
        Expression<Func<IQueryable<TResult>, IQueryable<TResult>>> queryExpression,
        CancellationToken cancellationToken = default) where TResult : notnull
    {
        return Execute();

        async IAsyncEnumerable<ShardItem<TResult>> Execute()
        {
            var channel = _channelCapacity.HasValue
                ? Channel.CreateBounded<ShardItem<TResult>>(new BoundedChannelOptions(_channelCapacity.Value)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait
                })
                : Channel.CreateUnbounded<ShardItem<TResult>>();

            var shardTasks = _shards.Select(shard => Task.Run(async () =>
            {
                var session = shard.CreateSession();
                var executor = shard.QueryExecutor;
                ShardStopReason reason = ShardStopReason.Completed;
                try
                {
                    var resultStream = executor.Execute(session, queryExpression);
                    await foreach (var item in resultStream.WithCancellation(cancellationToken))
                    {
                        await channel.Writer.WriteAsync(new ShardItem<TResult>(shard.ShardId, item), cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    reason = ShardStopReason.Canceled;
                }
                catch (Exception)
                {
                    reason = ShardStopReason.Faulted;
                    throw;
                }
                finally
                {
                    TryObserver(o => o.OnShardStopped(shard.ShardId, reason));
                }
            }, cancellationToken)).ToList();

            var completion = Task.WhenAll(shardTasks);
            _ = completion.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    channel.Writer.TryComplete(t.Exception);
                }
                else if (t.IsCanceled)
                {
                    channel.Writer.TryComplete(new OperationCanceledException(cancellationToken));
                }
                else
                {
                    channel.Writer.TryComplete();
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return item;
            }

            await completion.ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    [Obsolete("Use QueryAllShardsOrderedStreamingAsync or QueryAllShardsOrderedEagerAsync explicitly.")]
    public IAsyncEnumerable<ShardItem<TResult>> QueryAllShardsOrderedAsync<TResult, TKey>(
        Func<TSession, IAsyncEnumerable<TResult>> query,
        Func<TResult, TKey> keySelector,
        CancellationToken cancellationToken = default)
        where TKey : IComparable<TKey>
    {
        // Forward to eager for backwards compatibility.
        return QueryAllShardsOrderedEagerAsync(query, keySelector, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ShardItem<TResult>> QueryAllShardsOrderedStreamingAsync<TResult, TKey>(
        Func<TSession, IAsyncEnumerable<TResult>> query,
        Func<TResult, TKey> keySelector,
        int prefetchPerShard = 1,
        CancellationToken cancellationToken = default)
        where TKey : IComparable<TKey>
    {
    return Execute();

    async IAsyncEnumerable<ShardItem<TResult>> Execute()
        {
            var shardEnumerators = new List<IShardisAsyncEnumerator<TResult>>();
            var shardIds = new List<ShardId>();
            int shardIndex = 0;
            foreach (var shard in _shards)
            {
                var session = shard.CreateSession();
        var stream = query(session);
        shardEnumerators.Add(new ShardisAsyncShardEnumerator<TResult>(shard.ShardId, shardIndex++, stream.GetAsyncEnumerator(cancellationToken)));
                shardIds.Add(shard.ShardId);
            }
            if (prefetchPerShard < 1) throw new ArgumentOutOfRangeException(nameof(prefetchPerShard));
            var probe = new ObserverMergeProbe(Observer, _heapSampleEvery);
        await using var ordered = new ShardisAsyncOrderedEnumerator<TResult, TKey>(shardEnumerators, keySelector, prefetchPerShard, cancellationToken, probe);
            while (await ordered.MoveNextAsync().ConfigureAwait(false))
            {
                TryObserver(o => o.OnItemYielded(ordered.Current.ShardId));
                yield return ordered.Current;
            }
            foreach (var id in shardIds) { TryObserver(o => o.OnShardCompleted(id)); TryObserver(o => o.OnShardStopped(id, ShardStopReason.Completed)); }
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ShardItem<TResult>> QueryAllShardsOrderedEagerAsync<TResult, TKey>(
        Func<TSession, IAsyncEnumerable<TResult>> query,
        Func<TResult, TKey> keySelector,
        CancellationToken cancellationToken = default)
        where TKey : IComparable<TKey>
    {
    return Execute();

    async IAsyncEnumerable<ShardItem<TResult>> Execute()
        {
            var bufferTasks = _shards.Select(async (shard, idx) =>
            {
                var session = shard.CreateSession();
                var list = new List<TResult>();
        await foreach (var item in query(session).WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    list.Add(item);
                }
                return (shard.ShardId, idx, list);
            }).ToArray();

            var perShardBuffers = await Task.WhenAll(bufferTasks).ConfigureAwait(false);
            var shardEnumerators = new List<IShardisAsyncEnumerator<TResult>>();
            var shardIds = new List<ShardId>();
            foreach (var buf in perShardBuffers)
            {
        shardEnumerators.Add(new ShardisAsyncShardEnumerator<TResult>(buf.ShardId, buf.idx, ToAsyncEnumerable(buf.list).GetAsyncEnumerator(cancellationToken)));
                shardIds.Add(buf.ShardId);
            }
            var probe = new ObserverMergeProbe(Observer, _heapSampleEvery);
        await using var ordered = new ShardisAsyncOrderedEnumerator<TResult, TKey>(shardEnumerators, keySelector, prefetchPerShard: 1, cancellationToken, probe);
            while (await ordered.MoveNextAsync().ConfigureAwait(false))
            {
                TryObserver(o => o.OnItemYielded(ordered.Current.ShardId));
                yield return ordered.Current;
            }
            foreach (var id in shardIds) { TryObserver(o => o.OnShardCompleted(id)); TryObserver(o => o.OnShardStopped(id, ShardStopReason.Completed)); }

            static async IAsyncEnumerable<TResult> ToAsyncEnumerable(IEnumerable<TResult> source)
            {
                foreach (var i in source)
                {
                    yield return i;
                    await Task.Yield();
                }
            }
        }
    }

    /// <summary>
    /// Executes a query across all shards and projects each item to another shape.
    /// </summary>
    public IAsyncEnumerable<TProjected> QueryAndProjectAsync<TResult, TProjected>(
        Func<TSession, IAsyncEnumerable<TResult>> query,
        Func<TResult, TProjected> selector,
        CancellationToken cancellationToken = default)
    {
        return Execute();

        async IAsyncEnumerable<TProjected> Execute()
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(selector);

            await foreach (var item in QueryAllShardsAsync(query, cancellationToken).ConfigureAwait(false))
            {
                yield return selector(item.Item);
            }
        }
    }
}

internal sealed class ObserverMergeProbe : IOrderedMergeProbe
{
    private readonly IMergeObserver _observer;
    private readonly int _sampleEvery;
    private int _counter;
    public ObserverMergeProbe(IMergeObserver observer, int sampleEvery = 1)
    {
        _observer = observer;
        _sampleEvery = sampleEvery < 1 ? 1 : sampleEvery;
        _counter = 0;
    }
    public void OnHeapSize(int size)
    {
        if ((++_counter % _sampleEvery) != 0) { return; }
        try { _observer.OnHeapSizeSample(size); } catch { /* swallow */ }
    }
}

partial class ShardStreamBroadcaster<TShard, TSession>
{
    private void TryObserver(Action<IMergeObserver> invoke)
    {
        if (ReferenceEquals(Observer, NoOpMergeObserver.Instance)) { return; }
        try { invoke(Observer); } catch { /* observer faults must not impact pipeline */ }
    }
}