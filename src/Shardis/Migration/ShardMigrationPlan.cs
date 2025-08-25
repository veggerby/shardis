using Shardis.Model;

namespace Shardis.Migration;

/// <summary>
/// Represents a plan for migrating a set of shard keys to a new shard.
/// </summary>
public sealed class ShardMigrationPlan<TKey>(ShardId source, ShardId target, IEnumerable<ShardKey<TKey>> keys)
    where TKey : notnull, IEquatable<TKey>
{
    public ShardId SourceShardId { get; } = source;
    public ShardId TargetShardId { get; } = target;
    public IReadOnlyList<ShardKey<TKey>> Keys { get; } = keys.ToList();
}