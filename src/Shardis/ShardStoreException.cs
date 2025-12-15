using Shardis.Model;

namespace Shardis;

/// <summary>
/// Exception thrown when shard map store or persistence operations fail.
/// Common scenarios include key assignment conflicts, connection failures, or serialization issues.
/// </summary>
public sealed class ShardStoreException : ShardisException
{
    /// <summary>
    /// Gets the operation that failed (e.g., "TryAssign", "GetOrAdd", "Persist").
    /// </summary>
    public string? Operation { get; }

    /// <summary>
    /// Gets the shard ID involved in the operation, if applicable.
    /// </summary>
    public ShardId? ShardId { get; }

    /// <summary>
    /// Gets the number of retry attempts made before failure, if applicable.
    /// </summary>
    public int? AttemptCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardStoreException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ShardStoreException(string message)
        : this(message, null, null, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardStoreException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ShardStoreException(string message, Exception? innerException)
        : this(message, innerException, null, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardStoreException"/> class with diagnostic context.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="shardId">The shard ID involved in the operation.</param>
    /// <param name="attemptCount">The number of retry attempts made.</param>
    /// <param name="additionalContext">Additional diagnostic context.</param>
    public ShardStoreException(
        string message,
        Exception? innerException,
        string? operation,
        ShardId? shardId,
        int? attemptCount,
        IDictionary<string, object?>? additionalContext)
        : base(message, innerException, BuildContext(operation, shardId, attemptCount, additionalContext))
    {
        Operation = operation;
        ShardId = shardId;
        AttemptCount = attemptCount;
    }

    private static Dictionary<string, object?> BuildContext(
        string? operation,
        ShardId? shardId,
        int? attemptCount,
        IDictionary<string, object?>? additionalContext)
    {
        var context = new Dictionary<string, object?>();

        if (operation != null)
        {
            context["Operation"] = operation;
        }

        if (shardId.HasValue)
        {
            context["ShardId"] = shardId.Value.Value;
        }

        if (attemptCount.HasValue)
        {
            context["AttemptCount"] = attemptCount.Value;
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
