using Shardis.Model;

namespace Shardis.Migration;

/// <summary>
/// Represents a plan for migrating a set of shard keys to a new shard.
/// </summary>
public sealed class ShardMigrationPlan<TKey>(ShardId source, ShardId target, IEnumerable<ShardKey<TKey>> keys)
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>The originating shard of the migration.</summary>
    public ShardId SourceShardId { get; } = source;
    /// <summary>The destination shard that will own the migrated keys.</summary>
    public ShardId TargetShardId { get; } = target;
    /// <summary>The ordered collection of keys to migrate.</summary>
    public IReadOnlyList<ShardKey<TKey>> Keys { get; } = keys.ToList();
}