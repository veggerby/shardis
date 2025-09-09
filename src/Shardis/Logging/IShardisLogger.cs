using System.Collections.Concurrent;

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

/// <summary>No-op logger.</summary>
public sealed class NullShardisLogger : IShardisLogger
{
    /// <summary>Singleton instance.</summary>
    public static readonly NullShardisLogger Instance = new();
    private NullShardisLogger() { }
    /// <inheritdoc />
    public bool IsEnabled(ShardisLogLevel level) => false;
    /// <inheritdoc />
    public void Log(ShardisLogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? tags = null) { }
}

/// <summary>Simple in-memory logger (intended for tests).</summary>
public sealed class InMemoryShardisLogger : IShardisLogger
{
    private readonly ConcurrentQueue<(DateTimeOffset ts, ShardisLogLevel level, string msg)> _entries = new();
    private readonly ShardisLogLevel _min;
    /// <summary>Creates a new instance.</summary>
    public InMemoryShardisLogger(ShardisLogLevel min = ShardisLogLevel.Trace) => _min = min;
    /// <inheritdoc />
    public bool IsEnabled(ShardisLogLevel level) => level >= _min;
    /// <inheritdoc />
    public void Log(ShardisLogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? tags = null)
    {
        if (!IsEnabled(level)) return;
        if (exception != null) message += " | ex=" + exception.GetType().Name + ": " + exception.Message;
        _entries.Enqueue((DateTimeOffset.UtcNow, level, message));
    }
    /// <summary>Returns the buffered log entries as immutable snapshot strings.</summary>
    public IReadOnlyCollection<string> Entries => _entries.Select(e => $"{e.ts:o} {e.level}: {e.msg}").ToArray();
}