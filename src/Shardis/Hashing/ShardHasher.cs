using System.Security.Cryptography;
using System.Text;

namespace Shardis.Hashing;

public static class ShardHasher
{
    /// <summary>
    /// Computes a stable, consistent 32-bit hash of a shard key using SHA-256.
    /// </summary>
    public static uint HashString(string key)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        return HashBytes(bytes);
    }

    /// <summary>
    /// Computes a stable, consistent 32-bit hash of a shard key using SHA-256.
    /// </summary>
    public static uint HashBytes(byte[] bytes)
    {
        var hashBytes = SHA256.HashData(bytes);

        // Take first 4 bytes for simplicity
        var intHash = BitConverter.ToUInt32(hashBytes, 0);

        return intHash;
    }
}
