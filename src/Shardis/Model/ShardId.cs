namespace Shardis.Model;

/// <summary>
/// Represents a unique identifier for a shard.
/// </summary>
public readonly record struct ShardId
{
    /// <summary>
    /// Gets the value of the shard ID.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardId"/> struct.
    /// </summary>
    /// <param name="value">The value of the shard ID.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or whitespace.</exception>
    public ShardId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
        Value = value;
    }

    /// <summary>
    /// Returns the string representation of the shard ID.
    /// </summary>
    /// <returns>The value of the shard ID as a string.</returns>
    public override string ToString() => Value;
}
