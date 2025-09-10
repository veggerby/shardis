using System.Linq.Expressions;
#pragma warning disable CS1591 // Public members missing XML comment (file provides top-level summaries sufficient for current preview)
using Microsoft.Extensions.DependencyInjection;
using Shardis.Query.Execution;

namespace Shardis.Query;

/// <summary>
/// High-level entry point for initiating shard queries with ergonomic helpers.
/// Wraps an <see cref="IShardQueryExecutor"/> providing overloads matching typical usage patterns.
/// </summary>
public interface IShardQueryClient
{
    /// <summary>
    /// Begin a shard-wide query for <typeparamref name="T"/>.
    /// </summary>
    IShardQueryable<T> Query<T>();

    /// <summary>
    /// Begin a shard-wide query for <typeparamref name="T"/> applying optional filter and projection.
    /// A projection must be supplied when <typeparamref name="TResult"/> differs from <typeparamref name="T"/>.
    /// </summary>
    IShardQueryable<TResult> Query<T, TResult>(Expression<Func<T, bool>>? where = null,
                                               Expression<Func<T, TResult>>? select = null);
}

#pragma warning restore CS1591

/// <summary>
/// Default implementation of <see cref="IShardQueryClient"/> delegating to an underlying <see cref="IShardQueryExecutor"/>.
/// </summary>
public sealed class ShardQueryClient(IShardQueryExecutor executor) : IShardQueryClient
{
    private readonly IShardQueryExecutor _executor = executor ?? throw new ArgumentNullException(nameof(executor));

    /// <summary>
    /// Begin a shard-wide query for <typeparamref name="T"/> using the underlying executor.
    /// </summary>
    public IShardQueryable<T> Query<T>() => ShardQuery.For<T>(_executor);

    /// <summary>
    /// Begin a shard-wide query for <typeparamref name="T"/> optionally applying <paramref name="where"/> and projecting to <typeparamref name="TResult"/>.
    /// A projection is required when result type differs.
    /// </summary>
    public IShardQueryable<TResult> Query<T, TResult>(Expression<Func<T, bool>>? where = null,
                                                      Expression<Func<T, TResult>>? select = null)
    {
        var root = ShardQuery.For<T>(_executor);

        if (where is not null)
        {
            root = ShardQueryableExtensions.Where(root, where);
        }

        if (select is not null)
        {
            return ShardQueryableSelectExtensions.Select((IShardQueryable<T>)root, select);
        }

        if (typeof(TResult) == typeof(T))
        {
            return (IShardQueryable<TResult>)root;
        }

        throw new InvalidOperationException("Projection (select) must be supplied when the result type differs.");
    }
}

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
