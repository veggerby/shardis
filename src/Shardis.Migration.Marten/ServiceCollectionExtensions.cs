namespace Microsoft.Extensions.DependencyInjection;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Marten;
using Shardis.Migration.Marten.Verification;

/// <summary>
/// Marten migration provider DI extensions.
/// </summary>
public static class MartenMigrationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Marten migration data mover and document checksum verification strategy.
    /// </summary>
    public static IServiceCollection AddMartenMigrationSupport<TKey>(this IServiceCollection services)
        where TKey : notnull, IEquatable<TKey>
    {
        services.AddSingleton<IShardDataMover<TKey>, MartenDataMover<TKey>>();
        services.AddSingleton<IVerificationStrategy<TKey>, DocumentChecksumVerificationStrategy<TKey>>();
        return services;
    }
}