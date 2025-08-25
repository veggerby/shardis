using Microsoft.Extensions.DependencyInjection;

using Shardis.Hashing;
using Shardis.Instrumentation;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Querying;
using Shardis.Routing;

namespace Shardis;

/// <summary>
/// Service collection extensions for configuring Shardis core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers sharding services and a router for the specified shard/session/key types.
    /// </summary>
    /// <typeparam name="TShard">Shard implementation type.</typeparam>
    /// <typeparam name="TKey">Shard key underlying value type.</typeparam>
    /// <typeparam name="TSession">The session/context type hosted by each shard.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Delegate used to configure <see cref="ShardisOptions{TShard,TKey,TSession}"/>.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Existing registrations of <see cref="IShardMapStore{TKey}"/> and <see cref="IShardisMetrics"/> are preserved (not overridden).
    /// The router type is selected based on options: either a user provided factory, consistent hashing or the default router.
    /// </remarks>
    public static IServiceCollection AddShardis<TShard, TKey, TSession>(
        this IServiceCollection services,
        Action<ShardisOptions<TShard, TKey, TSession>> configure)
        where TShard : IShard<TSession>
        where TKey : notnull, IEquatable<TKey>
    {
        var options = new ShardisOptions<TShard, TKey, TSession>();
        configure(options);

        services.AddSingleton<IEnumerable<TShard>>(options.Shards);
        // Register selected router
        if (options.RouterFactory is not null)
        {
            services.AddSingleton<IShardRouter<TKey, TSession>>(sp => options.RouterFactory(sp, options.Shards));
        }
        else if (options.UseConsistentHashing)
        {
            services.AddSingleton<IShardRouter<TKey, TSession>>(sp =>
            {
                var store = sp.GetRequiredService<IShardMapStore<TKey>>();
                var hasher = sp.GetRequiredService<IShardKeyHasher<TKey>>();
                var ringHasher = options.RingHasher ?? DefaultShardRingHasher.Instance;
                return new ConsistentHashShardRouter<TShard, TKey, TSession>(store, options.Shards.Cast<TShard>(), hasher, options.ReplicationFactor, ringHasher);
            });
        }
        else
        {
            services.AddSingleton<IShardRouter<TKey, TSession>>(sp =>
            {
                var store = sp.GetService<IShardMapStore<TKey>>() ?? sp.GetRequiredService<IShardMapStore<TKey>>();
                var hasher = sp.GetRequiredService<IShardKeyHasher<TKey>>();
                return new DefaultShardRouter<TKey, TSession>(store, options.Shards.Cast<IShard<TSession>>(), hasher);
            });
        }
        services.AddSingleton<IShardBroadcaster<TSession>, ShardBroadcaster<TShard, TSession>>();
        services.AddSingleton<IShardStreamBroadcaster<TSession>, ShardStreamBroadcaster<TShard, TSession>>();
        // Map store registration (allow override)
        if (!services.Any(sd => sd.ServiceType == typeof(IShardMapStore<TKey>)))
        {
            if (options.ShardMapStoreFactory is not null)
            {
                services.AddSingleton<IShardMapStore<TKey>>(sp => options.ShardMapStoreFactory(sp));
            }
            else
            {
                services.AddSingleton<IShardMapStore<TKey>, InMemoryShardMapStore<TKey>>();
            }
        }

        services.AddSingleton<IShardKeyHasher<TKey>>(sp => options.ShardKeyHasher ?? DefaultShardKeyHasher<TKey>.Instance);
        services.AddSingleton<IShardisMetrics>(sp => NoOpShardisMetrics.Instance); // user can replace
        if (options.RingHasher is not null)
        {
            services.AddSingleton(options.RingHasher);
        }

        return services;
    }
}

/// <summary>
/// Options controlling Shardis registration and router behavior.
/// </summary>
/// <typeparam name="TShard">Shard implementation type.</typeparam>
/// <typeparam name="TKey">Shard key underlying value type.</typeparam>
/// <typeparam name="TSession">Session/context type.</typeparam>
public class ShardisOptions<TShard, TKey, TSession>
    where TShard : IShard<TSession>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Collection of shards to register. Must not be empty when building the provider.
    /// </summary>
    public IList<TShard> Shards { get; } = [];

    /// <summary>
    /// When <c>true</c> (default) a consistent hash ring router is used; otherwise <see cref="DefaultShardRouter{TKey, TSession}"/>.
    /// Ignored if <see cref="RouterFactory"/> is supplied.
    /// </summary>
    public bool UseConsistentHashing { get; set; } = true;

    /// <summary>
    /// Fully custom router factory. If provided it overrides <see cref="UseConsistentHashing"/> selection logic.
    /// </summary>
    public Func<IServiceProvider, IEnumerable<TShard>, IShardRouter<TKey, TSession>>? RouterFactory { get; set; }

    /// <summary>
    /// Factory for a custom shard map store. If null an in-memory implementation is used (unless one was pre-registered).
    /// </summary>
    public Func<IServiceProvider, IShardMapStore<TKey>>? ShardMapStoreFactory { get; set; }

    /// <summary>
    /// Override key hasher; defaults to <see cref="DefaultShardKeyHasher{TKey}"/>.
    /// </summary>
    public IShardKeyHasher<TKey>? ShardKeyHasher { get; set; }

    /// <summary>
    /// Optional ring hasher for consistent hashing (only used when <see cref="UseConsistentHashing"/> is true).
    /// </summary>
    public IShardRingHasher? RingHasher { get; set; }

    /// <summary>
    /// Virtual node replication factor for consistent hashing. Higher values yield smoother distribution with higher memory cost.
    /// </summary>
    public int ReplicationFactor { get; set; } = 100;
}