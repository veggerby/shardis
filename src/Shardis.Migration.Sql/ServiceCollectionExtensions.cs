namespace Microsoft.Extensions.DependencyInjection;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Sql;

/// <summary>
/// SQL migration durability DI extensions.
/// </summary>
public static class SqlMigrationServiceCollectionExtensions
{
    /// <summary>Adds a SQL-backed checkpoint store for the given key type.</summary>
    public static IServiceCollection AddSqlCheckpointStore<TKey>(this IServiceCollection services, Func<System.Data.Common.DbConnection> connectionFactory, string tableName = "ShardMigrationCheckpoint")
        where TKey : notnull, IEquatable<TKey>
    {
        services.AddSingleton<IShardMigrationCheckpointStore<TKey>>(_ => new SqlCheckpointStore<TKey>(connectionFactory, tableName));
        return services;
    }

    /// <summary>Adds a SQL-backed shard map store (non-provider-specific) for durability.</summary>
    public static IServiceCollection AddSqlShardMapStore<TKey>(this IServiceCollection services, Func<System.Data.Common.DbConnection> connectionFactory)
        where TKey : notnull, IEquatable<TKey>
    {
        services.AddSingleton<Shardis.Persistence.IShardMapStore<TKey>>(_ => new SqlShardMapStore<TKey>(connectionFactory));
        return services;
    }
}