using Microsoft.Extensions.DependencyInjection;

using Shardis.Migration.Abstractions;
using Shardis.Migration.EntityFrameworkCore.Verification;

namespace Shardis.Migration.EntityFrameworkCore;

/// <summary>DI extensions for EntityFrameworkCore migration provider.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers EntityFrameworkCore migration support: data mover + rowversion verifier (default).
    /// </summary>
    public static IServiceCollection AddEntityFrameworkCoreMigrationSupport<TKey, TContext, TEntity>(this IServiceCollection services)
        where TKey : notnull, IEquatable<TKey>
        where TContext : Microsoft.EntityFrameworkCore.DbContext
        where TEntity : class, IShardEntity<TKey>
    {
        // Data mover
        services.AddSingleton<IShardDataMover<TKey>, EntityFrameworkCoreDataMover<TKey, TContext, TEntity>>();

        // Default rowversion verification (can be replaced by user with checksum strategy registration)
        services.AddSingleton<IVerificationStrategy<TKey>, RowVersionVerificationStrategy<TKey, TContext, TEntity>>();

        return services;
    }

    /// <summary>Registers checksum verification strategy (opt-in, replaces default if resolved last).</summary>
    public static IServiceCollection AddEntityFrameworkCoreChecksumVerification<TKey, TContext, TEntity>(this IServiceCollection services)
        where TKey : notnull, IEquatable<TKey>
        where TContext : Microsoft.EntityFrameworkCore.DbContext
        where TEntity : class, IShardEntity<TKey>
    {
        services.AddSingleton<IVerificationStrategy<TKey>, ChecksumVerificationStrategy<TKey, TContext, TEntity>>();
        return services;
    }
}