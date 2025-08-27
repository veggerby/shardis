namespace Shardis.Migration.Abstractions;

using Shardis.Migration.Model;

/// <summary>
/// Performs data copy and verification for individual key moves.
/// </summary>
/// <typeparam name="TKey">Underlying key value type.</typeparam>
public interface IShardDataMover<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Copies data for the specified key move to the target shard.</summary>
    /// <param name="move">The key move to copy.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CopyAsync(KeyMove<TKey> move, CancellationToken ct);

    /// <summary>Verifies previously copied data; returns true if valid.</summary>
    /// <param name="move">The key move to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if verification succeeded; otherwise false.</returns>
    Task<bool> VerifyAsync(KeyMove<TKey> move, CancellationToken ct);
}