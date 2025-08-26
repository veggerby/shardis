using Shardis.Model;

namespace Shardis.Hashing;

/// <summary>
/// Computes a 32-bit stable hash for a <see cref="ShardKey{TKey}"/> enabling deterministic shard routing.
/// </summary>
/// <typeparam name="TKey">The underlying key value type.</typeparam>
public interface IShardKeyHasher<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Computes a stable 32-bit hash value for the provided <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The shard key to hash.</param>
    /// <returns>A 32-bit hash suitable for ring placement or modulo selection.</returns>
    uint ComputeHash(ShardKey<TKey> key);
}