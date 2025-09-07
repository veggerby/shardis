
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shardis.Factories;
using Shardis.Model;

namespace Shardis.DependencyInjection;

/// <summary>
/// Fluent registration helpers for per-shard session / resource factories.
/// </summary>
public static class ShardServiceCollectionExtensions
{
    private static IServiceCollection EnsureShardInfra<T>(this IServiceCollection services)
    {
        services.TryAddSingleton<IPerShardRegistry<T>, PerShardRegistry<T>>();
        services.TryAddSingleton<IShardFactory<T>, DependencyInjectionShardFactory<T>>();
        return services;
    }

    /// <summary>
    /// Registers a specific shard with a pre-created instance.
    /// </summary>
    /// <typeparam name="T">Resource type.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="shard">Shard identifier.</param>
    /// <param name="instance">Existing instance to reuse for this shard.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddShardInstance<T>(this IServiceCollection services, ShardId shard, T instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        services.EnsureShardInfra<T>();
        services.PostConfigure<IPerShardRegistry<T>>(r => r.Add(shard, (_, _) => new ValueTask<T>(instance)));
        return services;
    }

    /// <summary>
    /// Registers a specific shard with a synchronous creation delegate.
    /// </summary>
    public static IServiceCollection AddShard<T>(this IServiceCollection services, ShardId shard, Func<ShardId, T> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        services.EnsureShardInfra<T>();
        services.PostConfigure<IPerShardRegistry<T>>(r => r.Add(shard, (_, sid) => new ValueTask<T>(create(sid))));
        return services;
    }

    /// <summary>
    /// Registers a specific shard with an asynchronous creation delegate.
    /// </summary>
    public static IServiceCollection AddShard<T>(this IServiceCollection services, ShardId shard, Func<IServiceProvider, ShardId, ValueTask<T>> createAsync)
    {
        ArgumentNullException.ThrowIfNull(createAsync);
        services.EnsureShardInfra<T>();
        services.PostConfigure<IPerShardRegistry<T>>(r => r.Add(shard, createAsync));
        return services;
    }

    /// <summary>
    /// Registers a contiguous set of shard identifiers [0..count-1] using a synchronous creation delegate.
    /// </summary>
    public static IServiceCollection AddShards<T>(this IServiceCollection services, int count, Func<ShardId, T> create)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        ArgumentNullException.ThrowIfNull(create);
        services.EnsureShardInfra<T>();
        for (var i = 0; i < count; i++)
        {
            var sid = new ShardId(i.ToString());
            services.PostConfigure<IPerShardRegistry<T>>(r => r.Add(sid, (_, __) => new ValueTask<T>(create(sid))));
        }
        return services;
    }

    /// <summary>
    /// Registers a contiguous set of shard identifiers [0..count-1] using an asynchronous creation delegate.
    /// </summary>
    public static IServiceCollection AddShards<T>(this IServiceCollection services, int count, Func<IServiceProvider, ShardId, ValueTask<T>> createAsync)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        ArgumentNullException.ThrowIfNull(createAsync);
        services.EnsureShardInfra<T>();
        for (var i = 0; i < count; i++)
        {
            var sid = new ShardId(i.ToString());
            services.PostConfigure<IPerShardRegistry<T>>(r => r.Add(sid, createAsync));
        }
        return services;
    }

    /// <summary>
    /// Resolves the set of shard ids registered for the specified resource type.
    /// </summary>
    public static IReadOnlyCollection<ShardId> GetRegisteredShards<T>(this IServiceProvider sp)
        => sp.GetRequiredService<IPerShardRegistry<T>>().Shards.ToArray();
}