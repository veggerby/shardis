using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Shardis.Factories;
using Shardis.Query.Execution;

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
}
