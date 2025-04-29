using Shardis.Model;

namespace Shardis.Migration;

/// <summary>
/// Provides a default implementation of the <see cref="IShardMigrator"/> interface for migrating data between shards.
/// </summary>
public class DefaultShardMigrator<TKey, TSession> : IShardMigrator<TKey, TSession>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Migrates data from one shard to another.
    /// </summary>
    /// <param name="sourceShard">The source shard from which data will be migrated.</param>
    /// <param name="targetShard">The target shard to which data will be migrated.</param>
    /// <param name="shardKey">The shard key representing the data to migrate.</param>
    /// <returns>A task that represents the asynchronous migration operation.</returns>
    public async Task MigrateAsync(IShard<TSession> sourceShard, IShard<TSession> targetShard, ShardKey<TKey> shardKey)
    {
        if (sourceShard == null) throw new ArgumentNullException(nameof(sourceShard));
        if (targetShard == null) throw new ArgumentNullException(nameof(targetShard));
        if (shardKey.Value == null) throw new ArgumentNullException(nameof(shardKey));

        // Simulate data migration logic
        await Task.Run(() =>
        {
            Console.WriteLine($"Migrating data for key '{shardKey}' from shard '{sourceShard.ShardId}' to shard '{targetShard.ShardId}'.");
        });
    }
}