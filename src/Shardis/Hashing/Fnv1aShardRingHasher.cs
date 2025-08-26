using System.Text;

namespace Shardis.Hashing;

/// <summary>
/// Fast non-cryptographic FNV-1a 32-bit implementation for ring hashing when throughput is favored.
/// </summary>
/// <remarks>
/// Produces a stable 32-bit value with good dispersion for typical shard key distributions. Slightly weaker
/// avalanche characteristics than SHA-256 but significantly cheaper for very hot routing paths.
/// </remarks>
public sealed class Fnv1aShardRingHasher : IShardRingHasher
{
    /// <summary>Gets a singleton instance.</summary>
    public static readonly IShardRingHasher Instance = new Fnv1aShardRingHasher();
    private Fnv1aShardRingHasher() { }

    /// <inheritdoc />
    public uint Hash(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        uint hash = offset;
        var span = Encoding.UTF8.GetBytes(value);
        for (int i = 0; i < span.Length; i++)
        {
            hash ^= span[i];
            hash *= prime;
        }
        return hash;
    }
}