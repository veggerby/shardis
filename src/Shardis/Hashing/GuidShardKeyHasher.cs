using Shardis.Model;

namespace Shardis.Hashing;

internal sealed class GuidShardKeyHasher : IShardKeyHasher<Guid>
{
    private GuidShardKeyHasher() { }

    public static readonly IShardKeyHasher<Guid> Instance = new GuidShardKeyHasher();

    public uint ComputeHash(ShardKey<Guid> key)
    {
        var bytes = key.Value.ToByteArray();
        return ShardHasher.HashBytes(bytes);
    }
}