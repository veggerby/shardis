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
        if (sourceShard == null)
        {
            throw new ArgumentNullException(nameof(sourceShard));
        }

        if (targetShard == null)
        {
            throw new ArgumentNullException(nameof(targetShard));
        }

        if (shardKey.Value == null)
        {
            throw new ArgumentNullException(nameof(shardKey));
        }

        // TODO: Implement actual data copy + reassignment strategy.
        await Task.CompletedTask;
    }

    public Task<ShardMigrationPlan<TKey>> PlanAsync(IShard<TSession> sourceShard, IShard<TSession> targetShard, IEnumerable<ShardKey<TKey>> keys)
    {
        ArgumentNullException.ThrowIfNull(sourceShard);
        ArgumentNullException.ThrowIfNull(targetShard);
        ArgumentNullException.ThrowIfNull(keys);
        return Task.FromResult(new ShardMigrationPlan<TKey>(sourceShard.ShardId, targetShard.ShardId, keys));
    }

    public async Task ExecutePlanAsync(ShardMigrationPlan<TKey> plan, Func<ShardKey<TKey>, Task>? perKeyCallback = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        foreach (var key in plan.Keys)
        {
            // Currently just invokes callback; real impl would copy data + update mapping.
            if (perKeyCallback != null)
            {
                await perKeyCallback(key).ConfigureAwait(false);
            }
        }
    }
}