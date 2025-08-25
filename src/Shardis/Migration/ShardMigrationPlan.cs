using Shardis.Model;

namespace Shardis.Migration;

/// <summary>
/// Represents a plan for migrating a set of shard keys to a new shard.
/// </summary>
public sealed class ShardMigrationPlan<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    public ShardId SourceShardId { get; }
    public ShardId TargetShardId { get; }
    public IReadOnlyList<ShardKey<TKey>> Keys { get; }

    public ShardMigrationPlan(ShardId source, ShardId target, IEnumerable<ShardKey<TKey>> keys)
    {
        SourceShardId = source;
        TargetShardId = target;
        Keys = keys.ToList();
    }
}