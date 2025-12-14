using Shardis.Model;

namespace Shardis;

/// <summary>
/// Exception thrown when shard query execution operations fail.
/// Common scenarios include query timeout, shard unavailability, or projection errors.
/// </summary>
public sealed class ShardQueryException : ShardisException
{
    /// <summary>
    /// Gets the query phase where the failure occurred (e.g., "Execution", "Projection", "Merge").
    /// </summary>
    public string? Phase { get; }

    /// <summary>
    /// Gets the shard ID where the query failed, if applicable.
    /// </summary>
    public ShardId? ShardId { get; }

    /// <summary>
    /// Gets the total number of shards targeted by the query, if applicable.
    /// </summary>
    public int? TargetedShardCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardQueryException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ShardQueryException(string message)
        : this(message, null, null, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardQueryException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ShardQueryException(string message, Exception? innerException)
        : this(message, innerException, null, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardQueryException"/> class with diagnostic context.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="phase">The query phase where the failure occurred.</param>
    /// <param name="shardId">The shard ID where the query failed.</param>
    /// <param name="targetedShardCount">The total number of shards targeted.</param>
    /// <param name="additionalContext">Additional diagnostic context.</param>
    public ShardQueryException(
        string message,
        Exception? innerException,
        string? phase,
        ShardId? shardId,
        int? targetedShardCount,
        IDictionary<string, object?>? additionalContext)
        : base(message, innerException, BuildContext(phase, shardId, targetedShardCount, additionalContext))
    {
        Phase = phase;
        ShardId = shardId;
        TargetedShardCount = targetedShardCount;
    }

    private static Dictionary<string, object?> BuildContext(
        string? phase,
        ShardId? shardId,
        int? targetedShardCount,
        IDictionary<string, object?>? additionalContext)
    {
        var context = new Dictionary<string, object?>();

        if (phase != null)
        {
            context["Phase"] = phase;
        }

        if (shardId.HasValue)
        {
            context["ShardId"] = shardId.Value.Value;
        }

        if (targetedShardCount.HasValue)
        {
            context["TargetedShardCount"] = targetedShardCount.Value;
        }

        if (additionalContext != null)
        {
            foreach (var kvp in additionalContext)
            {
                context[kvp.Key] = kvp.Value;
            }
        }

        return context;
    }
}
