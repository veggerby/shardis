using System.Collections.ObjectModel;

namespace Shardis;

/// <summary>
/// Base exception for all Shardis framework errors.
/// Provides structured diagnostic context to aid troubleshooting.
/// </summary>
public class ShardisException : Exception
{
    /// <summary>
    /// Gets diagnostic context associated with this exception.
    /// Contains key-value pairs describing the failure context (e.g., shard ID, key hash, topology version).
    /// </summary>
    public IReadOnlyDictionary<string, object?> DiagnosticContext { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardisException"/> class.
    /// </summary>
    public ShardisException()
        : this(null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardisException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ShardisException(string? message)
        : this(message, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardisException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ShardisException(string? message, Exception? innerException)
        : this(message, innerException, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardisException"/> class with a specified error message, inner exception, and diagnostic context.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <param name="diagnosticContext">Optional diagnostic context providing additional failure details.</param>
    public ShardisException(string? message, Exception? innerException, IDictionary<string, object?>? diagnosticContext)
        : base(message, innerException)
    {
        DiagnosticContext = diagnosticContext != null
            ? new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(diagnosticContext))
            : new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
    }
}
