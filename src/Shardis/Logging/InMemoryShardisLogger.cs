using System.Collections.Concurrent;

namespace Shardis.Logging;

/// <summary>Simple in-memory logger (intended for tests).</summary>
/// <remarks>Creates a new instance.</remarks>
public sealed class InMemoryShardisLogger(ShardisLogLevel min = ShardisLogLevel.Trace) : IShardisLogger
{
    private readonly ConcurrentQueue<(DateTimeOffset ts, ShardisLogLevel level, string msg)> _entries = new();
    private readonly ShardisLogLevel _min = min;

    /// <inheritdoc />
    public bool IsEnabled(ShardisLogLevel level) => level >= _min;

    /// <inheritdoc />
    public void Log(ShardisLogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? tags = null)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        if (exception != null)
        {
            message += " | ex=" + exception.GetType().Name + ": " + exception.Message;
        }

        _entries.Enqueue((DateTimeOffset.UtcNow, level, message));
    }

    /// <summary>Returns the buffered log entries as immutable snapshot strings.</summary>
    public IReadOnlyCollection<string> Entries => _entries.Select(e => $"{e.ts:o} {e.level}: {e.msg}").ToArray();
}