using System.Linq.Expressions;
using System.Threading.Channels;

using Shardis.Model;

namespace Shardis.Querying;

/// <summary>
/// Provides an implementation of the <see cref="IShardStreamBroadcaster{TSession}"/> interface for querying all shards in parallel.
/// </summary>
/// <typeparam name="TSession">The type of session used for querying shards.</typeparam>
public class ShardStreamBroadcaster<TShard, TSession> : IShardStreamBroadcaster<TSession> where TShard : IShard<TSession>
{
    private readonly IEnumerable<TShard> _shards;
    private readonly int? _channelCapacity;

    public ShardStreamBroadcaster(IEnumerable<TShard> shards, int? channelCapacity = null)
    {
        ArgumentNullException.ThrowIfNull(shards, nameof(shards));
        if (!shards.Any())
        {
            throw new ArgumentException("Shard collection must not be empty", nameof(shards));
        }

        _shards = shards;
        _channelCapacity = channelCapacity;
    }

    public async IAsyncEnumerable<ShardItem<TResult>> QueryAllShardsAsync<TResult>(
        Func<TSession, IAsyncEnumerable<TResult>> query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
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
            try
            {
                await foreach (var item in query(session).WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    await channel.Writer.WriteAsync(new(shard.ShardId, item), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // swallow expected cancellation
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
            yield return item;
        }

        // Observe producer exceptions
        await completion.ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an expression-based query (LINQ transformation) across all shards using backend-aware executors.
    /// </summary>
    public async IAsyncEnumerable<ShardItem<TResult>> QueryAllShardsWithExpressionAsync<TResult>(
        Expression<Func<IQueryable<TResult>, IQueryable<TResult>>> queryExpression,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) where TResult : notnull
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

    /// <summary>
    /// Executes a globally ordered query using a LINQ ordering expression and backend-native execution.
    /// </summary>
    // NOTE: This method fulfills the interface by projecting from IAsyncEnumerable query rather than expression for now.
    public async IAsyncEnumerable<ShardItem<TResult>> QueryAllShardsOrderedAsync<TResult, TKey>(
        Func<TSession, IAsyncEnumerable<TResult>> query,
        Func<TResult, TKey> keySelector,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TKey : IComparable<TKey>
    {
        // Simple approach: gather per shard, then k-way merge
        var shardEnumerators = new List<IShardisAsyncEnumerator<TResult>>();
        foreach (var shard in _shards)
        {
            var session = shard.CreateSession();
            var stream = query(session);
            shardEnumerators.Add(new ShardisAsyncShardEnumerator<TResult>(shard.ShardId, stream.GetAsyncEnumerator(cancellationToken)));
        }

        await using var ordered = new ShardisAsyncOrderedEnumerator<TResult, TKey>(shardEnumerators, keySelector, cancellationToken);

        while (await ordered.MoveNextAsync().ConfigureAwait(false))
        {
            yield return ordered.Current;
        }
    }

    public async IAsyncEnumerable<TProjected> QueryAndProjectAsync<TResult, TProjected>(
        Func<TSession, IAsyncEnumerable<TResult>> query,
        Func<TResult, TProjected> selector,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(selector);

        await foreach (var item in QueryAllShardsAsync(query, cancellationToken).ConfigureAwait(false))
        {
            yield return selector(item.Item);
        }
    }
}