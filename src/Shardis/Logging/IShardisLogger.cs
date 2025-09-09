namespace Shardis.Logging;

/// <summary>
/// Minimal structured logger used internally by Shardis to avoid a hard dependency on any specific logging framework.
/// Implementations MUST be thread-safe. Library components only log when an implementation is registered.
/// </summary>
public interface IShardisLogger
{
    /// <summary>Returns true if <paramref name="level"/> is enabled.</summary>
    bool IsEnabled(ShardisLogLevel level);
    /// <summary>Writes a log entry.</summary>
    /// <param name="level">Severity level.</param>
    /// <param name="message">Formatted message.</param>
    /// <param name="exception">Optional exception.</param>
    /// <param name="tags">Optional structured tags (implementation may ignore).</param>
    void Log(ShardisLogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? tags = null);
}