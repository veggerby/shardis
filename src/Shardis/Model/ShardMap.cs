namespace Shardis.Model;

/// <summary>
/// Represents a mapping between a shard key and a shard ID.
/// </summary>
public readonly record struct ShardMap<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Gets the shard key associated with the mapping.
    /// </summary>
    public ShardKey<TKey> ShardKey { get; }

    /// <summary>
    /// Gets the shard ID associated with the mapping.
    /// </summary>
    public ShardId ShardId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardMap{TKey}"/> struct.
    /// </summary>
    /// <param name="shardKey">The shard key to map.</param>
    /// <param name="shardId">The shard ID to map to.</param>
    public ShardMap(ShardKey<TKey> shardKey, ShardId shardId)
    {
        ShardKey = shardKey;
        ShardId = shardId;
    }

    /// <summary>
    /// Returns the string representation of the shard mapping.
    /// </summary>
    /// <returns>A string in the format "ShardKey -> ShardId".</returns>
    public override string ToString() => $"{ShardKey} -> {ShardId}";
}