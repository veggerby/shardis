namespace Shardis.Migration.Abstractions;

using Shardis.Migration.Model;

/// <summary>
/// Applies a batch of verified key moves atomically (all-or-nothing logical effect).
/// </summary>
/// <typeparam name="TKey">Underlying key value type.</typeparam>
public interface IShardMapSwapper<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Swaps ownership for a batch of verified key moves. Implementations should achieve atomicity per batch
    /// or provide compensating actions to ensure no partial visibility.
    /// </summary>
    /// <param name="verifiedBatch">The verified key moves to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SwapAsync(IReadOnlyList<KeyMove<TKey>> verifiedBatch, CancellationToken ct);
}