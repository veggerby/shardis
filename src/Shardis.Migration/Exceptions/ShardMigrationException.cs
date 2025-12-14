using Shardis;
using Shardis.Model;

namespace Shardis.Migration.Exceptions;

/// <summary>
/// Exception thrown when shard migration operations fail.
/// Common scenarios include copy failures, verification errors, swap failures, or checkpoint persistence issues.
/// </summary>
public sealed class ShardMigrationException : ShardisException
{
    /// <summary>
    /// Gets the migration phase where the failure occurred (e.g., "Copy", "Verify", "Swap", "Checkpoint").
    /// </summary>
    public string? Phase { get; }

    /// <summary>
    /// Gets the source shard ID involved in the migration, if applicable.
    /// </summary>
    public ShardId? SourceShardId { get; }

    /// <summary>
    /// Gets the target shard ID involved in the migration, if applicable.
    /// </summary>
    public ShardId? TargetShardId { get; }

    /// <summary>
    /// Gets the number of retry attempts made before failure, if applicable.
    /// </summary>
    public int? AttemptCount { get; }

    /// <summary>
    /// Gets the migration plan ID associated with this operation, if applicable.
    /// </summary>
    public string? PlanId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardMigrationException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ShardMigrationException(string message)
        : this(message, null, null, null, null, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardMigrationException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ShardMigrationException(string message, Exception? innerException)
        : this(message, innerException, null, null, null, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardMigrationException"/> class with diagnostic context.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="phase">The migration phase where the failure occurred.</param>
    /// <param name="sourceShardId">The source shard ID.</param>
    /// <param name="targetShardId">The target shard ID.</param>
    /// <param name="attemptCount">The number of retry attempts made.</param>
    /// <param name="planId">The migration plan ID.</param>
    /// <param name="additionalContext">Additional diagnostic context.</param>
    public ShardMigrationException(
        string message,
        Exception? innerException,
        string? phase,
        ShardId? sourceShardId,
        ShardId? targetShardId,
        int? attemptCount,
        string? planId,
        IDictionary<string, object?>? additionalContext)
        : base(message, innerException, BuildContext(phase, sourceShardId, targetShardId, attemptCount, planId, additionalContext))
    {
        Phase = phase;
        SourceShardId = sourceShardId;
        TargetShardId = targetShardId;
        AttemptCount = attemptCount;
        PlanId = planId;
    }

    private static Dictionary<string, object?> BuildContext(
        string? phase,
        ShardId? sourceShardId,
        ShardId? targetShardId,
        int? attemptCount,
        string? planId,
        IDictionary<string, object?>? additionalContext)
    {
        var context = new Dictionary<string, object?>();

        if (phase != null)
        {
            context["Phase"] = phase;
        }

        if (sourceShardId.HasValue)
        {
            context["SourceShardId"] = sourceShardId.Value.Value;
        }

        if (targetShardId.HasValue)
        {
            context["TargetShardId"] = targetShardId.Value.Value;
        }

        if (attemptCount.HasValue)
        {
            context["AttemptCount"] = attemptCount.Value;
        }

        if (planId != null)
        {
            context["PlanId"] = planId;
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
