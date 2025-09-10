using Microsoft.Extensions.DependencyInjection;

using Shardis.Query.Execution;

namespace Shardis.Query;

/// <summary>
/// Service collection extensions for registering the query client abstraction.
/// </summary>
public static class QueryClientServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="IShardQueryClient"/> backed by an existing <see cref="IShardQueryExecutor"/>.
    /// Does not register an executor; caller is responsible for configuring one (e.g. EF Core or in-memory).
    /// Safe no-op if a client is already registered.
    /// </summary>
    public static IServiceCollection AddShardisQueryClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(d => d.ServiceType == typeof(IShardQueryClient)))
        {
            return services; // respect existing registration
        }

        services.AddSingleton<IShardQueryClient, ShardQueryClient>();
        return services;
    }
}