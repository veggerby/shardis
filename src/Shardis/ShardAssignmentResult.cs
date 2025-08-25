using Shardis.Model;

namespace Shardis;

/// <summary>
/// Represents the outcome of a shard routing attempt including the resolved shard
/// and whether the mapping already existed prior to this routing operation.
/// </summary>
/// <typeparam name="TSession">The shard session type.</typeparam>
public readonly record struct ShardAssignmentResult<TSession>
{
    /// <summary>
    /// The shard selected for the key.
    /// </summary>
    public IShard<TSession> Shard { get; }

    /// <summary>
    /// True if the shard was already previously assigned (lookup hit), false if a new assignment was created.
    /// </summary>
    public bool WasExistingAssignment { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardAssignmentResult{TSession}"/> struct.
    /// </summary>
    /// <param name="shard">Resolved shard.</param>
    /// <param name="wasExisting">True when an existing assignment was found; false when a new assignment was created.</param>
    public ShardAssignmentResult(IShard<TSession> shard, bool wasExisting)
    {
        Shard = shard;
        WasExistingAssignment = wasExisting;
    }
}