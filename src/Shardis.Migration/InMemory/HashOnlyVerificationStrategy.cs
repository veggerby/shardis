
using System.Security.Cryptography;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;

namespace Shardis.Migration.InMemory;
/// <summary>
/// Placeholder hash-only verification strategy: computes a deterministic hash of key + target shard.
/// Always returns true unless a mismatch injector signals false for a given move.
/// </summary>
internal sealed class HashOnlyVerificationStrategy<TKey> : IVerificationStrategy<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    public Func<KeyMove<TKey>, bool>? MismatchInjector { get; set; }

    public Task<bool> VerifyAsync(KeyMove<TKey> move, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (MismatchInjector?.Invoke(move) == true)
        {
            return Task.FromResult(false);
        }

        // Compute stable hash (not currently used in decision, placeholder for future optimization instrumentation).
        _ = StableHash(move);
        return Task.FromResult(true);
    }

    private static ulong StableHash(KeyMove<TKey> move)
    {
        var raw = $"{move.Key.Value}|{move.Target.Value}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return BitConverter.ToUInt64(hash, 0);
    }
}