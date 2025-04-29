using Shardis.Model;

namespace Shardis.Hashing;

internal sealed class UInt32ShardKeyHasher : IShardKeyHasher<uint>
{
    private UInt32ShardKeyHasher() { }

    public static readonly IShardKeyHasher<uint> Instance = new UInt32ShardKeyHasher();

    public uint ComputeHash(ShardKey<uint> key)
    {
        var bytes = BitConverter.GetBytes(key.Value);
        return ShardHasher.HashBytes(bytes);
    }
}
