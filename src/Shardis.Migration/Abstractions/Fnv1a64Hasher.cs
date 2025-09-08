namespace Shardis.Migration.Abstractions;

/// <summary>Default FNV-1a 64-bit hasher implementation (non-crypto, fast).</summary>
public sealed class Fnv1a64Hasher : IStableHasher
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    /// <inheritdoc />
    public ulong Hash(ReadOnlySpan<byte> data)
    {
        ulong h = Offset;

        for (int i = 0; i < data.Length; i++)
        {
            h ^= data[i];
            h *= Prime;
        }

        return h;
    }
}
