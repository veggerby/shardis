using System.Text;

namespace Shardis.Hashing;

/// <summary>
/// FNV-1a 32-bit implementation for faster non-cryptographic ring hashing.
/// </summary>
public sealed class Fnv1aShardRingHasher : IShardRingHasher
{
    public static readonly IShardRingHasher Instance = new Fnv1aShardRingHasher();
    private Fnv1aShardRingHasher() { }

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