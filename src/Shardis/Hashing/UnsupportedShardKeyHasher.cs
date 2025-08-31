using Shardis.Model;

namespace Shardis.Hashing;

internal sealed class UnsupportedShardKeyHasher<TKey> : IShardKeyHasher<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private UnsupportedShardKeyHasher() { }

    public static readonly IShardKeyHasher<TKey> Instance = new UnsupportedShardKeyHasher<TKey>();

    public uint ComputeHash(ShardKey<TKey> key) =>
        throw new ShardisException($"No shard hasher is registered for type {typeof(TKey)}.");
}