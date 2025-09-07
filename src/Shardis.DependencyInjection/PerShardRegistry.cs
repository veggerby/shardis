
using System.Collections.Concurrent;

using Shardis.Model;

namespace Shardis.DependencyInjection;

internal sealed class PerShardRegistry<T> : IPerShardRegistry<T>
{
    private readonly ConcurrentDictionary<ShardId, Func<IServiceProvider, ShardId, ValueTask<T>>> _map = new();

    public void Add(ShardId shard, Func<IServiceProvider, ShardId, ValueTask<T>> creator)
    {
        if (!_map.TryAdd(shard, creator))
        {
            throw new InvalidOperationException($"Shard '{shard}' is already registered for {typeof(T).Name}.");
        }
    }

    public bool Contains(ShardId shard) => _map.ContainsKey(shard);
    public IEnumerable<ShardId> Shards => _map.Keys;

    public Func<IServiceProvider, ShardId, ValueTask<T>> Get(ShardId shard) =>
        _map.TryGetValue(shard, out var c)
            ? c
            : throw new KeyNotFoundException($"No registration for shard '{shard}' and {typeof(T).Name}.");
}