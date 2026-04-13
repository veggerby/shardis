using System.Buffers;
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
    private const uint OFFSET = 2_166_136_261;
    private const uint PRIME = 16_777_619;

    // Strings up to this byte-length are hashed using a stack-allocated buffer to avoid heap pressure.
    private const int StackAllocThreshold = 256;

    /// <summary>Gets a singleton instance.</summary>
    public static readonly IShardRingHasher Instance = new Fnv1aShardRingHasher();
    private Fnv1aShardRingHasher() { }

    /// <inheritdoc />
    public uint Hash(string value)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);

        if (maxByteCount <= StackAllocThreshold)
        {
            Span<byte> buffer = stackalloc byte[maxByteCount];
            int written = Encoding.UTF8.GetBytes(value, buffer);
            return HashBytes(buffer[..written]);
        }

        byte[] rented = ArrayPool<byte>.Shared.Rent(maxByteCount);
        try
        {
            int written = Encoding.UTF8.GetBytes(value, rented);
            return HashBytes(rented.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static uint HashBytes(ReadOnlySpan<byte> bytes)
    {
        uint hash = OFFSET;
        for (int i = 0; i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash *= PRIME;
        }

        return hash;
    }
}