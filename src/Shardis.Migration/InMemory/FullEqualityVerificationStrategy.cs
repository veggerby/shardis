namespace Shardis.Migration.InMemory;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;

/// <summary>
/// Verification strategy that simply delegates to the underlying data mover's verification.
/// </summary>
internal sealed class FullEqualityVerificationStrategy<TKey> : IVerificationStrategy<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly IShardDataMover<TKey> _mover;

    public FullEqualityVerificationStrategy(IShardDataMover<TKey> mover)
    {
        _mover = mover;
    }

    public Task<bool> VerifyAsync(KeyMove<TKey> move, CancellationToken ct) => _mover.VerifyAsync(move, ct);
}