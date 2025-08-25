namespace Shardis.Hashing;

/// <summary>
/// Default ring hasher delegating to ShardHasher (SHA-256 truncated).
/// </summary>
public sealed class DefaultShardRingHasher : IShardRingHasher
{
    public static readonly IShardRingHasher Instance = new DefaultShardRingHasher();
    private DefaultShardRingHasher() { }
    public uint Hash(string value) => ShardHasher.HashString(value);
}