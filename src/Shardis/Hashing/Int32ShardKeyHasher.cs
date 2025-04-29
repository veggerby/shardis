using Shardis.Model;

namespace Shardis.Hashing;

internal sealed class Int32ShardKeyHasher : IShardKeyHasher<int>
{
    private Int32ShardKeyHasher() { }

    public static readonly IShardKeyHasher<int> Instance = new Int32ShardKeyHasher();

    public uint ComputeHash(ShardKey<int> key)
    {
        var bytes = BitConverter.GetBytes(key.Value);
        return ShardHasher.HashBytes(bytes);
    }
}
