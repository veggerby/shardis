namespace Shardis;

/// <summary>
/// Represents errors that occur within the Shardis framework.
/// </summary>
public class ShardisException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShardisException"/> class.
    /// </summary>
    public ShardisException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardisException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ShardisException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardisException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ShardisException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}