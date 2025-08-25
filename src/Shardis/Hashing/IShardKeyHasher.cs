using Shardis.Model;

namespace Shardis.Hashing;

public interface IShardKeyHasher<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    uint ComputeHash(ShardKey<TKey> key);
}