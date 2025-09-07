
using Shardis.Factories;
using Shardis.Model;

namespace Shardis.DependencyInjection;

internal sealed class DependencyInjectionShardFactory<T>(IServiceProvider sp, IPerShardRegistry<T> registry) : IShardFactory<T>
{
    public T Create(ShardId shard) => CreateAsync(shard).GetAwaiter().GetResult();

    public ValueTask<T> CreateAsync(ShardId shard, CancellationToken ct = default)
    {
        var creator = registry.Get(shard);
        return creator(sp, shard);
    }
}