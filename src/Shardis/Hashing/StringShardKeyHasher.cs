using Shardis.Model;

namespace Shardis.Hashing;

/// <summary>
/// Hashes <see cref="string"/> shard keys using SHA-256 (truncated) via <see cref="ShardHasher"/>.
/// </summary>
internal sealed class StringShardKeyHasher : IShardKeyHasher<string>
{
    private StringShardKeyHasher() { }

    /// <summary>Gets a singleton instance.</summary>
    public static readonly IShardKeyHasher<string> Instance = new StringShardKeyHasher();

    /// <inheritdoc />
    public uint ComputeHash(ShardKey<string> key) => ShardHasher.HashString(key.Value);
}