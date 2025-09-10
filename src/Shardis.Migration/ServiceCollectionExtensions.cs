using Microsoft.Extensions.DependencyInjection;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Execution;
using Shardis.Migration.InMemory;
using Shardis.Migration.Instrumentation;
using Shardis.Migration.Throttling;
using Shardis.Persistence;

namespace Shardis.Migration;

/// <summary>
/// Service collection extensions for registering Shardis key migration services.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static IServiceCollection RegisterMigrationCore<TKey>(IServiceCollection services, ShardMigrationOptions options)
        where TKey : notnull, IEquatable<TKey>
    {
        // Replace existing options registration intentionally.
        services.AddSingleton(options);

        static bool IsRegistered(IServiceCollection svc, Type t) => svc.Any(sd => sd.ServiceType == t);

        if (!IsRegistered(services, typeof(IShardMigrationPlanner<TKey>)))
        {
            services.AddSingleton<IShardMigrationPlanner<TKey>, InMemoryMigrationPlanner<TKey>>();
        }

        if (!IsRegistered(services, typeof(IShardMapStore<TKey>)))
        {
            services.AddSingleton<IShardMapStore<TKey>, InMemoryShardMapStore<TKey>>();
        }

        if (!IsRegistered(services, typeof(IShardDataMover<TKey>)))
        {
            services.AddSingleton<IShardDataMover<TKey>, InMemoryDataMover<TKey>>();
        }

        if (!IsRegistered(services, typeof(IVerificationStrategy<TKey>)))
        {
            services.AddSingleton<IVerificationStrategy<TKey>, FullEqualityVerificationStrategy<TKey>>();
        }

        if (!IsRegistered(services, typeof(IShardMapSwapper<TKey>)))
        {
            services.AddSingleton<IShardMapSwapper<TKey>, InMemoryMapSwapper<TKey>>();
        }

        if (!IsRegistered(services, typeof(IShardMigrationCheckpointStore<TKey>)))
        {
            services.AddSingleton<IShardMigrationCheckpointStore<TKey>, InMemoryCheckpointStore<TKey>>();
        }

        if (!IsRegistered(services, typeof(IShardMigrationMetrics)))
        {
            services.AddSingleton<IShardMigrationMetrics, NoOpShardMigrationMetrics>();
        }

        if (!IsRegistered(services, typeof(IEntityProjectionStrategy)))
        {
            services.AddSingleton<IEntityProjectionStrategy>(NoOpEntityProjectionStrategy.Instance);
        }

        if (!IsRegistered(services, typeof(IStableHasher)))
        {
            services.AddSingleton<IStableHasher, Fnv1a64Hasher>();
        }

        if (!IsRegistered(services, typeof(IStableCanonicalizer)))
        {
            services.AddSingleton<IStableCanonicalizer, JsonStableCanonicalizer>();
        }

        if (!IsRegistered(services, typeof(IBudgetGovernor)))
        {
            services.AddSingleton<IBudgetGovernor>(sp => new SimpleBudgetGovernor(
                initialGlobal: options.MaxConcurrentMoves ?? 256,
                maxPerShard: options.MaxMovesPerShard ?? 16));
        }

        services.AddTransient<ShardMigrationExecutor<TKey>>();
        return services;
    }

    /// <summary>
    /// Adds shard key migration services using an explicitly constructed <see cref="ShardMigrationOptions"/> instance.
    /// Use this overload to configure init-only properties via object initializer.
    /// </summary>
    public static IServiceCollection AddShardisMigration<TKey>(
        this IServiceCollection services,
        ShardMigrationOptions options)
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(options);
        return RegisterMigrationCore<TKey>(services, options);
    }
    /// <summary>
    /// Adds shard key migration services (planner, mover, verification, swapper, checkpoint store, metrics) using in-memory defaults.
    /// Existing registrations for the interfaces are preserved (not overridden).
    /// </summary>
    /// <typeparam name="TKey">Underlying shard key value type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration delegate for <see cref="ShardMigrationOptions"/>.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Default implementations are intended for tests / samples only. Production scenarios should provide durable implementations
    /// for the data mover and checkpoint store, and a metrics implementation if observability is required.
    /// </remarks>
    public static IServiceCollection AddShardisMigration<TKey>(
        this IServiceCollection services,
        Action<ShardMigrationOptions>? configure = null)
        where TKey : notnull, IEquatable<TKey>
    {
    var options = new ShardMigrationOptions();
    configure?.Invoke(options); // cannot set init-only properties here but retained for backwards compatibility
    return RegisterMigrationCore<TKey>(services, options);
    }
}