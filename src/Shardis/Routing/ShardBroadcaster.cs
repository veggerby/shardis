using System.Collections.Concurrent;

using Shardis.Model;

namespace Shardis.Routing;

/// <summary>
/// Provides an implementation of the <see cref="IShardBroadcaster{TSession}"/> interface for querying all shards in parallel.
/// </summary>
/// <typeparam name="TSession">The type of session used for querying shards.</typeparam>
public class ShardBroadcaster<TShard, TSession> : IShardBroadcaster<TSession> where TShard : IShard<TSession>
{
    private readonly IEnumerable<TShard> _shards;
    private readonly int _maxDegreeOfParallelism = 20;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardBroadcaster{TSession}"/> class.
    /// </summary>
    /// <param name="shards">The collection of shards to query.</param>
    /// <param name="maxDegreeOfParallelism">The maximum degree of parallelism for querying shards.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shards"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxDegreeOfParallelism"/> is less than or equal to zero.</exception>
    public ShardBroadcaster(IEnumerable<TShard> shards, int maxDegreeOfParallelism = 20)
    {
        _shards = shards ?? throw new ArgumentNullException(nameof(shards));
        _maxDegreeOfParallelism = maxDegreeOfParallelism > 0 ? maxDegreeOfParallelism : throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), "Max degree of parallelism must be greater than zero.");
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<TResult>> QueryAllShardsAsync<TResult>(Func<TSession, Task<IEnumerable<TResult>>> query, CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var results = new ConcurrentBag<TResult>();

        await Parallel.ForEachAsync(_shards, new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism, CancellationToken = cancellationToken }, async (shard, ct) =>
        {
            var session = shard.CreateSession();
            var partialResults = await query(session).ConfigureAwait(false);
            foreach (var result in partialResults)
            {
                results.Add(result);
            }
        }).ConfigureAwait(false);

        return results;
    }
}