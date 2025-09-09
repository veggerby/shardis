namespace Shardis.Logging;

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