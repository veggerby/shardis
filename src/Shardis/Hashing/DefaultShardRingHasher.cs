namespace Shardis.Hashing;

/// <summary>
/// Default ring hasher delegating to <see cref="ShardHasher"/> (SHA-256 truncated to 32 bits).
/// </summary>
/// <remarks>
/// Provides a balance between uniform distribution and computational cost. For higher throughput at the
/// expense of weaker avalanche properties, use <see cref="Fnv1aShardRingHasher"/>.
/// </remarks>
public sealed class DefaultShardRingHasher : IShardRingHasher
{
    /// <summary>Gets a singleton instance.</summary>
    public static readonly IShardRingHasher Instance = new DefaultShardRingHasher();

    private DefaultShardRingHasher() { }

    /// <inheritdoc />
    public uint Hash(string value) => ShardHasher.HashString(value);
}