using Shardis.Model;

namespace Shardis.Hashing;

/// <summary>
/// Hashes <see cref="int"/> shard keys by converting to bytes and delegating to <see cref="ShardHasher"/>.
/// </summary>
internal sealed class Int32ShardKeyHasher : IShardKeyHasher<int>
{
    private Int32ShardKeyHasher() { }

    /// <summary>Gets a singleton instance.</summary>
    public static readonly IShardKeyHasher<int> Instance = new Int32ShardKeyHasher();

    /// <inheritdoc />
    public uint ComputeHash(ShardKey<int> key)
    {
        var bytes = BitConverter.GetBytes(key.Value);
        return ShardHasher.HashBytes(bytes);
    }
}