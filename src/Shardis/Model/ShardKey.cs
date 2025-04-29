namespace Shardis.Model;

/// <summary>
/// Represents a key used for sharding, ensuring deterministic routing to shards.
/// </summary>
public readonly record struct ShardKey<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Gets the value of the shard key.
    /// </summary>
    public TKey Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardKey"/> struct.
    /// </summary>
    /// <param name="value">The value of the shard key.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or whitespace.</exception>
    public ShardKey(TKey value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));
        Value = value;
    }

    /// <summary>
    /// Returns the string representation of the shard key.
    /// </summary>
    /// <returns>The value of the shard key as a string.</returns>
    public override string ToString() => Value.ToString() ?? string.Empty;
}
