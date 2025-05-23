using Shardis.Model;

namespace Shardis.Migration;

/// <summary>
/// Defines the contract for migrating data between shards.
/// </summary>
public interface IShardMigrator<TKey, TSession> where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Migrates data from one shard to another.
    /// </summary>
    /// <param name="sourceShard">The source shard from which data will be migrated.</param>
    /// <param name="targetShard">The target shard to which data will be migrated.</param>
    /// <param name="shardKey">The shard key representing the data to migrate.</param>
    /// <returns>A task that represents the asynchronous migration operation.</returns>
    Task MigrateAsync(IShard<TSession> sourceShard, IShard<TSession> targetShard, ShardKey<TKey> shardKey);
}