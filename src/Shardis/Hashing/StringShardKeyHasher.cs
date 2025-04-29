using Shardis.Model;

namespace Shardis.Hashing;

internal sealed class StringShardKeyHasher : IShardKeyHasher<string>
{
    private StringShardKeyHasher() { }

    public static readonly IShardKeyHasher<string> Instance = new StringShardKeyHasher();

    public uint ComputeHash(ShardKey<string> key) => ShardHasher.HashString(key.Value);
}
