
using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;

namespace Shardis.Migration.InMemory;
/// <summary>
/// Verification strategy that simply delegates to the underlying data mover's verification.
/// </summary>
internal sealed class FullEqualityVerificationStrategy<TKey>(IShardDataMover<TKey> mover) : IVerificationStrategy<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly IShardDataMover<TKey> _mover = mover;

    public Task<bool> VerifyAsync(KeyMove<TKey> move, CancellationToken ct) => _mover.VerifyAsync(move, ct);
}