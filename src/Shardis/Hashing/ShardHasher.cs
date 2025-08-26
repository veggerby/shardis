using System.Security.Cryptography;
using System.Text;

namespace Shardis.Hashing;

/// <summary>
/// Utility helpers for producing stable 32-bit hash codes from arbitrary byte or string inputs.
/// </summary>
/// <remarks>
/// Uses SHA-256 and truncates the first 4 bytes providing a uniform distribution adequate for
/// shard routing while remaining deterministic across processes and versions.
/// </remarks>
public static class ShardHasher
{
    /// <summary>
    /// Computes a stable, consistent 32-bit hash of the provided <paramref name="key"/> string using SHA-256.
    /// </summary>
    /// <param name="key">The string to hash (UTF-8 encoded).</param>
    /// <returns>A 32-bit hash code.</returns>
    public static uint HashString(string key)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        return HashBytes(bytes);
    }

    /// <summary>
    /// Computes a stable, consistent 32-bit hash of the provided <paramref name="bytes"/> using SHA-256.
    /// </summary>
    /// <param name="bytes">The bytes to hash.</param>
    /// <returns>A 32-bit hash code.</returns>
    public static uint HashBytes(byte[] bytes)
    {
        var hashBytes = SHA256.HashData(bytes);
        var intHash = BitConverter.ToUInt32(hashBytes, 0); // take first 4 bytes
        return intHash;
    }
}