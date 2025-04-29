using Microsoft.Extensions.DependencyInjection;

using Shardis.Hashing;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Querying;
using Shardis.Routing;

namespace Shardis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShardis<TShard, TKey, TSession>(
        this IServiceCollection services,
        Action<ShardisOptions<TShard, TSession>> configure)
        where TShard : IShard<TSession>
        where TKey : notnull, IEquatable<TKey>
    {
        var options = new ShardisOptions<TShard, TSession>();
        configure(options);

        services.AddSingleton<IEnumerable<TShard>>(options.Shards);
        services.AddSingleton<IShardRouter<TKey, TSession>, ConsistentHashShardRouter<TShard, TKey, TSession>>();
        services.AddSingleton<IShardBroadcaster<TSession>, ShardBroadcaster<TShard, TSession>>();
        services.AddSingleton<IShardStreamBroadcaster<TSession>, ShardStreamBroadcaster<TShard, TSession>>();
        services.AddSingleton<IShardMapStore<TKey>, InMemoryShardMapStore<TKey>>();

        services.AddSingleton(DefaultShardKeyHasher<TKey>.Instance);

        return services;
    }
}

public class ShardisOptions<TShard, TSession> where TShard : IShard<TSession>
{
    public IList<TShard> Shards { get; } = [];
}