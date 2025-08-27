namespace Shardis.Migration.Model;

using Shardis.Model;

/// <summary>
/// Represents a persisted snapshot of migration progress used for resuming after interruption.
/// </summary>
/// <typeparam name="TKey">The underlying key value type.</typeparam>
public sealed record MigrationCheckpoint<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>The migration plan identifier.</summary>
    public Guid PlanId { get; }

    /// <summary>The checkpoint schema version.</summary>
    public int Version { get; }

    /// <summary>The timestamp (UTC) when this checkpoint was produced.</summary>
    public DateTimeOffset UpdatedAtUtc { get; }

    /// <summary>The states of all tracked shard keys.</summary>
    public IReadOnlyDictionary<ShardKey<TKey>, KeyMoveState> States { get; }

    /// <summary>The last processed move index (inclusive) or -1 if none.</summary>
    public int LastProcessedIndex { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationCheckpoint{TKey}"/> class.
    /// Performs a defensive copy of the provided state dictionary.
    /// </summary>
    public MigrationCheckpoint(
        Guid planId,
        int version,
        DateTimeOffset updatedAtUtc,
        IReadOnlyDictionary<ShardKey<TKey>, KeyMoveState> states,
        int lastProcessedIndex)
    {
        PlanId = planId;
        Version = version;
        UpdatedAtUtc = updatedAtUtc;
        States = states.Count == 0 ? [] : new Dictionary<ShardKey<TKey>, KeyMoveState>(states);
        LastProcessedIndex = lastProcessedIndex;
    }
}