namespace Shardis.Migration.Abstractions;

/// <summary>Provides a stable non-cryptographic 64-bit hash.</summary>
public interface IStableHasher
{
    /// <summary>Computes a deterministic 64-bit non-cryptographic hash for the provided bytes.</summary>
    /// <param name="data">The input data span.</param>
    /// <returns>Unsigned 64-bit hash value.</returns>
    ulong Hash(ReadOnlySpan<byte> data);
}