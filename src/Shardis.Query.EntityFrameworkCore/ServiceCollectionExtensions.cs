using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Shardis.Factories;
using Shardis.Query.Execution;
using Shardis.Query.Execution.FailureHandling;

namespace Shardis.Query.EntityFrameworkCore;

/// <summary>DI helpers for EF Core shard query executor registration.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register unordered EF Core shard query execution and expose <see cref="IShardQueryExecutor"/>.
    /// </summary>
    public static IServiceCollection AddShardisEfCoreUnordered<TContext>(this IServiceCollection services,
                                                                         int shardCount,
                                                                         IShardFactory<TContext> contextFactory,
                                                                         EfCoreExecutionOptions? options = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(contextFactory);

        services.AddSingleton<IShardQueryExecutor>(_ => EfCoreShardQueryExecutor.CreateUnordered(shardCount, contextFactory, options));
        return services;
    }

    /// <summary>
    /// Register ordered (buffered) EF Core shard query execution and expose <see cref="IShardQueryExecutor"/>.
    /// Use only for bounded result sets.
    /// </summary>
    public static IServiceCollection AddShardisEfCoreOrdered<TContext, TOrder>(this IServiceCollection services,
                                                                               int shardCount,
                                                                               IShardFactory<TContext> contextFactory,
                                                                               Expression<Func<TOrder, object>> orderKey,
                                                                               bool descending = false,
                                                                               EfCoreExecutionOptions? options = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(orderKey);

        services.AddSingleton<IShardQueryExecutor>(_ => EfCoreShardQueryExecutor.CreateOrdered<TContext, TOrder>(shardCount, contextFactory, orderKey, descending, options));
        return services;
    }

    /// <summary>
    /// Decorate existing registered <see cref="IShardQueryExecutor"/> with a failure strategy wrapper.
    /// No-op if no executor registration exists yet.
    /// </summary>
    public static IServiceCollection DecorateShardQueryFailureStrategy(this IServiceCollection services,
                                                                       IShardQueryFailureStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(strategy);

        var descriptor = services.LastOrDefault(s => s.ServiceType == typeof(IShardQueryExecutor));
        if (descriptor is null)
        {
            return services;
        }

        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(typeof(IShardQueryExecutor), sp =>
        {
            var inner = (IShardQueryExecutor)(descriptor.ImplementationInstance
                ?? descriptor.ImplementationFactory?.Invoke(sp)
                ?? ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!));
            return new FailureHandlingExecutor(inner, strategy);
        }, descriptor.Lifetime));
        return services;
    }
}