namespace Shardis;

/// <summary>
/// Exception thrown when shard topology validation or operations fail.
/// Common scenarios include duplicate keys, topology snapshot limit exceeded, or invalid topology state.
/// </summary>
public sealed class ShardTopologyException : ShardisException
{
    /// <summary>
    /// Gets the topology version associated with the failure, if applicable.
    /// </summary>
    public long? TopologyVersion { get; }

    /// <summary>
    /// Gets the number of keys in the topology snapshot, if applicable.
    /// </summary>
    public int? KeyCount { get; }

    /// <summary>
    /// Gets the maximum allowed key count for the topology, if applicable.
    /// </summary>
    public int? MaxKeyCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardTopologyException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ShardTopologyException(string message)
        : this(message, null, null, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardTopologyException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ShardTopologyException(string message, Exception? innerException)
        : this(message, innerException, null, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardTopologyException"/> class with diagnostic context.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="topologyVersion">The topology version associated with the failure.</param>
    /// <param name="keyCount">The number of keys in the topology snapshot.</param>
    /// <param name="maxKeyCount">The maximum allowed key count.</param>
    /// <param name="additionalContext">Additional diagnostic context.</param>
    public ShardTopologyException(
        string message,
        Exception? innerException,
        long? topologyVersion,
        int? keyCount,
        int? maxKeyCount,
        IDictionary<string, object?>? additionalContext)
        : base(message, innerException, BuildContext(topologyVersion, keyCount, maxKeyCount, additionalContext))
    {
        TopologyVersion = topologyVersion;
        KeyCount = keyCount;
        MaxKeyCount = maxKeyCount;
    }

    private static Dictionary<string, object?> BuildContext(
        long? topologyVersion,
        int? keyCount,
        int? maxKeyCount,
        IDictionary<string, object?>? additionalContext)
    {
        var context = new Dictionary<string, object?>();

        if (topologyVersion.HasValue)
        {
            context["TopologyVersion"] = topologyVersion.Value;
        }

        if (keyCount.HasValue)
        {
            context["KeyCount"] = keyCount.Value;
        }

        if (maxKeyCount.HasValue)
        {
            context["MaxKeyCount"] = maxKeyCount.Value;
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
