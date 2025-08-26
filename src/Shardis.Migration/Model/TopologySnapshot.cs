namespace Shardis.Migration.Model;

using Shardis.Model;

/// <summary>
/// Represents a snapshot of shard ownership for a set of keys at a point in time.
/// This is a minimal placeholder; a richer topology model can replace it later.
/// </summary>
public sealed class TopologySnapshot<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Gets the assignments (key -> shard id).</summary>
    public IReadOnlyDictionary<ShardKey<TKey>, ShardId> Assignments { get; }

    /// <summary>Initializes a new instance of the <see cref="TopologySnapshot{TKey}"/> class.</summary>
    public TopologySnapshot(IReadOnlyDictionary<ShardKey<TKey>, ShardId> assignments)
    {
        Assignments = assignments.Count == 0 ? new Dictionary<ShardKey<TKey>, ShardId>() : new Dictionary<ShardKey<TKey>, ShardId>(assignments);
    }
}