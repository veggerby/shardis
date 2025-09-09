namespace Shardis.Logging.Console;

/// <summary>
/// Simple console logger implementation of <see cref="IShardisLogger"/> intended for samples, development and diagnostics.
/// Production deployments should prefer an adapter to Microsoft.Extensions.Logging or structured sinks.
/// </summary>
public sealed class ConsoleShardisLogger(ShardisLogLevel minimumLevel = ShardisLogLevel.Information) : IShardisLogger
{
    private readonly ShardisLogLevel _min = minimumLevel;

    /// <inheritdoc />
    public bool IsEnabled(ShardisLogLevel level) => level >= _min;

    /// <inheritdoc />
    public void Log(ShardisLogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? tags = null)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        var ts = DateTimeOffset.UtcNow.ToString("o");
        var line = $"{ts} [{level}] {message}";
        if (tags is { Count: > 0 })
        {
            var kv = string.Join(" ", tags.Select(kv => kv.Key + '=' + (kv.Value ?? "null")));
            line += " | " + kv;
        }

        System.Console.WriteLine(line);
        if (exception != null)
        {
            System.Console.WriteLine(exception);
        }
    }
}