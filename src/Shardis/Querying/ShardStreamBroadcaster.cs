using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Shardis.Model;

namespace Shardis.Querying;

public class ShardStreamBroadcaster<TShard, TSession> : IShardStreamBroadcaster<TSession> where TShard : IShard<TSession>
{
    private readonly IEnumerable<TShard> _shards;

    public ShardStreamBroadcaster(IEnumerable<TShard> shards)
    {
        ArgumentNullException.ThrowIfNull(shards, nameof(shards));

        _shards = shards;
    }

    public async IAsyncEnumerable<ShardItem<TResult>> QueryAllShardsAsync<TResult>(
        Func<TSession, IAsyncEnumerable<TResult>> query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query, nameof(query));

        var channel = Channel.CreateUnbounded<ShardItem<TResult>>();

        var shardTasks = _shards.Select(async shard =>
        {
            var session = shard.CreateSession();
            await foreach (var item in query(session).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await channel.Writer.WriteAsync(new(shard.ShardId, item), cancellationToken).ConfigureAwait(false);
            }
        }).ToList();

        var background = Task.WhenAll(shardTasks).ContinueWith(_ => channel.Writer.Complete(), cancellationToken);

        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }

        await background.ConfigureAwait(false);
    }
}
