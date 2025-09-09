using Shardis.Logging;

namespace Shardis.Migration.Durable.Sample;

internal sealed class ConsoleShardisLogger(ShardisLogLevel min = ShardisLogLevel.Information) : IShardisLogger
{
    private readonly ShardisLogLevel _min = min;
    public bool IsEnabled(ShardisLogLevel level) => level >= _min;
    public void Log(ShardisLogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? tags = null)
    {
        if (!IsEnabled(level)) return;
        Console.WriteLine($"[{level}] {message}");
        if (exception != null) Console.WriteLine(exception);
    }
}