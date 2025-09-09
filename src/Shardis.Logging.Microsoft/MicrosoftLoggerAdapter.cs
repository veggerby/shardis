using Microsoft.Extensions.Logging;

namespace Shardis.Logging.Microsoft;

/// <summary>
/// Adapts an <see cref="ILogger"/> to the <see cref="IShardisLogger"/> abstraction.
/// </summary>
/// <remarks>
/// Creates a new adapter.
/// </remarks>
/// <param name="logger">Underlying Microsoft logger.</param>
/// <param name="levelMap">Optional mapping override from <see cref="ShardisLogLevel"/> to <see cref="LogLevel"/>.</param>
public sealed class MicrosoftLoggerAdapter(ILogger logger, Func<ShardisLogLevel, LogLevel>? levelMap = null) : IShardisLogger
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Func<ShardisLogLevel, LogLevel> _map = levelMap ?? DefaultMap;

    /// <inheritdoc />
    public bool IsEnabled(ShardisLogLevel level) => _logger.IsEnabled(_map(level));

    /// <inheritdoc />
    public void Log(ShardisLogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? tags = null)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        var logLevel = _map(level);
        if (tags is { Count: > 0 })
        {
            using var scope = _logger.BeginScope(tags.ToDictionary(k => k.Key, v => v.Value));
            _logger.Log(logLevel, new EventId(), message, exception, static (s, _) => s);
            return;
        }

        _logger.Log(logLevel, new EventId(), message, exception, static (s, _) => s);
    }

    private static LogLevel DefaultMap(ShardisLogLevel level) => level switch
    {
        ShardisLogLevel.Trace => LogLevel.Trace,
        ShardisLogLevel.Debug => LogLevel.Debug,
        ShardisLogLevel.Information => LogLevel.Information,
        ShardisLogLevel.Warning => LogLevel.Warning,
        ShardisLogLevel.Error => LogLevel.Error,
        ShardisLogLevel.Critical => LogLevel.Critical,
        _ => LogLevel.Information
    };
}

/// <summary>
/// Extension helpers to register the Microsoft logger adapter.
/// </summary>
public static class ShardisMicrosoftLoggingExtensions
{
    /// <summary>
    /// Wraps an existing <see cref="ILoggerFactory"/> creating an <see cref="IShardisLogger"/> instance.
    /// </summary>
    public static IShardisLogger CreateShardisLogger(this ILoggerFactory factory, string category = "Shardis", Func<ShardisLogLevel, LogLevel>? levelMap = null)
    {
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        return new MicrosoftLoggerAdapter(factory.CreateLogger(category), levelMap);
    }
}