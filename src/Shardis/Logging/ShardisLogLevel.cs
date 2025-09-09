namespace Shardis.Logging;

/// <summary>Severity levels for Shardis logging abstraction.</summary>
public enum ShardisLogLevel
{
    /// <summary>Fine grained diagnostic events (high volume).</summary>
    Trace = 0,
    /// <summary>Development / debug information.</summary>
    Debug = 1,
    /// <summary>General informational events.</summary>
    Information = 2,
    /// <summary>Recoverable issues or unexpected events.</summary>
    Warning = 3,
    /// <summary>Errors that prevent an operation from completing.</summary>
    Error = 4,
    /// <summary>Critical failures requiring immediate attention.</summary>
    Critical = 5,
}