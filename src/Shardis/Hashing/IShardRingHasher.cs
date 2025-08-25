namespace Shardis.Hashing;

/// <summary>
/// Provides hashing for virtual nodes on the consistent hash ring.
/// </summary>
public interface IShardRingHasher
{
    uint Hash(string value);
}