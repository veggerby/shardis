
using Shardis.Model;

namespace Shardis.DependencyInjection;

internal interface IPerShardRegistry<T>
{
    void Add(ShardId shard, Func<IServiceProvider, ShardId, ValueTask<T>> creator);
    bool Contains(ShardId shard);
    IEnumerable<ShardId> Shards { get; }
    Func<IServiceProvider, ShardId, ValueTask<T>> Get(ShardId shard);
}