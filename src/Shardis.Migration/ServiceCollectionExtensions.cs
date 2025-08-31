using Microsoft.Extensions.DependencyInjection;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Execution;
using Shardis.Migration.InMemory;
using Shardis.Migration.Instrumentation;
using Shardis.Persistence;

namespace Shardis.Migration;

/// <summary>
/// Service collection extensions for registering Shardis key migration services.
/// </summary>
public static class ServiceCollectionExtensions
{
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
        configure?.Invoke(options);
        // Register options instance (replace any previous registration intentionally).
        services.AddSingleton(options);

        // Helper local function to detect existing registrations for a given service type.
        static bool IsRegistered(IServiceCollection svc, Type t) => svc.Any(sd => sd.ServiceType == t);

        if (!IsRegistered(services, typeof(IShardMigrationPlanner<TKey>)))
        {
            services.AddSingleton<IShardMigrationPlanner<TKey>, InMemoryMigrationPlanner<TKey>>();
        }

        // Ensure a shard map store exists (normally provided by core AddShardis registration). For isolated migration usage
        // scenarios we fall back to an in-memory map store.
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

        // Internal executor can be resolved via DI inside tests (InternalsVisibleTo) / adapters later.
        services.AddTransient<ShardMigrationExecutor<TKey>>();

        return services;
    }
}