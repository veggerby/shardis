using Microsoft.Extensions.DependencyInjection;

using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;

namespace Shardis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShardis<TShard, TSession>(
        this IServiceCollection services,
        Action<ShardisOptions<TShard, TSession>> configure) where TShard : IShard<TSession>
    {
        var options = new ShardisOptions<TShard, TSession>();
        configure(options);

        services.AddSingleton<IEnumerable<TShard>>(options.Shards);
        services.AddSingleton<IShardRouter<TSession>, ConsistentHashShardRouter<TShard, TSession>>();
        services.AddSingleton<IShardBroadcaster<TSession>, ShardBroadcaster<TShard, TSession>>();
        services.AddSingleton<IShardStreamBroadcaster<TSession>, ShardStreamBroadcaster<TShard, TSession>>();
        services.AddSingleton<IShardMapStore, InMemoryShardMapStore>();

        return services;
    }
}

public class ShardisOptions<TShard, TSession> where TShard : IShard<TSession>
{
    public IList<TShard> Shards { get; } = [];
}