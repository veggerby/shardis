using Shardis.Model;

namespace Shardis;

/// <summary>
/// Exception thrown when shard routing operations fail.
/// Common scenarios include duplicate shard IDs, empty shard rings, or invalid routing configurations.
/// </summary>
public sealed class ShardRoutingException : ShardisException
{
    /// <summary>
    /// Gets the shard ID associated with the routing failure, if applicable.
    /// </summary>
    public ShardId? ShardId { get; }

    /// <summary>
    /// Gets the key hash value involved in the routing operation, if applicable.
    /// </summary>
    public uint? KeyHash { get; }

    /// <summary>
    /// Gets the total number of shards available during routing, if applicable.
    /// </summary>
    public int? ShardCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardRoutingException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ShardRoutingException(string message)
        : this(message, null, null, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardRoutingException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ShardRoutingException(string message, Exception? innerException)
        : this(message, innerException, null, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardRoutingException"/> class with diagnostic context.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="shardId">The shard ID associated with the failure.</param>
    /// <param name="keyHash">The key hash value involved in the operation.</param>
    /// <param name="shardCount">The total number of shards available.</param>
    /// <param name="additionalContext">Additional diagnostic context.</param>
    public ShardRoutingException(
        string message,
        Exception? innerException,
        ShardId? shardId,
        uint? keyHash,
        int? shardCount,
        IDictionary<string, object?>? additionalContext)
        : base(message, innerException, BuildContext(shardId, keyHash, shardCount, additionalContext))
    {
        ShardId = shardId;
        KeyHash = keyHash;
        ShardCount = shardCount;
    }

    private static Dictionary<string, object?> BuildContext(
        ShardId? shardId,
        uint? keyHash,
        int? shardCount,
        IDictionary<string, object?>? additionalContext)
    {
        var context = new Dictionary<string, object?>();

        if (shardId.HasValue)
        {
            context["ShardId"] = shardId.Value.Value;
        }

        if (keyHash.HasValue)
        {
            context["KeyHash"] = keyHash.Value.ToString("X8");
        }

        if (shardCount.HasValue)
        {
            context["ShardCount"] = shardCount.Value;
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
