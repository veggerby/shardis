using Shardis.Model;

namespace Shardis.Hashing;

internal sealed class Int64ShardKeyHasher : IShardKeyHasher<long>
{
    private Int64ShardKeyHasher() { }

    public static readonly IShardKeyHasher<long> Instance = new Int64ShardKeyHasher();

    public uint ComputeHash(ShardKey<long> key)
    {
        var bytes = BitConverter.GetBytes(key.Value);
        return ShardHasher.HashBytes(bytes);
    }
}