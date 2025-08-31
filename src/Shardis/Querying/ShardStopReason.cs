namespace Shardis.Querying;

/// <summary>
/// Reason a shard stream stopped.
/// </summary>
public enum ShardStopReason
{
    /// <summary>Shard enumerated all items successfully.</summary>
    Completed = 0,
    /// <summary>Enumeration canceled due to external cancellation token.</summary>
    Canceled = 1,
    /// <summary>Enumeration terminated because of an unhandled exception.</summary>
    Faulted = 2
}