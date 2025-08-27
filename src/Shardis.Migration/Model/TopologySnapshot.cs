namespace Shardis.Migration.Model;

using Shardis.Model;

/// <summary>
/// Represents a snapshot of shard ownership for a set of keys at a point in time.
/// This is a minimal placeholder; a richer topology model can replace it later.
/// </summary>
/// <remarks>
/// Initial design driven by ADR 0002; future enhancements may introduce range-based or segmented snapshots.
/// Initializes a new instance of the <see cref="TopologySnapshot{TKey}"/> class.
/// </remarks>
public sealed class TopologySnapshot<TKey>(IReadOnlyDictionary<ShardKey<TKey>, ShardId> assignments)
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Gets the assignments (key -> shard id).</summary>
    public IReadOnlyDictionary<ShardKey<TKey>, ShardId> Assignments { get; } = assignments.Count == 0 ? [] : new Dictionary<ShardKey<TKey>, ShardId>(assignments);
}