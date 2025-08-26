namespace Shardis.Hashing;

/// <summary>
/// Provides hashing for virtual nodes on the consistent hash ring.
/// </summary>
public interface IShardRingHasher
{
    /// <summary>
    /// Computes a 32-bit hash value for the supplied <paramref name="value"/> used for ring placement.
    /// </summary>
    /// <param name="value">The string value (e.g. shard id + replica index) to hash.</param>
    /// <returns>A 32-bit hash suitable for ordering ring entries.</returns>
    uint Hash(string value);
}