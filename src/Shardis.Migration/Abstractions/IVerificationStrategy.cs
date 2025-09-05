
using Shardis.Migration.Model;

namespace Shardis.Migration.Abstractions;
/// <summary>
/// Strategy for verifying copied key data.
/// </summary>
/// <typeparam name="TKey">Underlying key value type.</typeparam>
public interface IVerificationStrategy<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>Performs verification for the given key move.</summary>
    /// <param name="move">The key move to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if data for the key is valid.</returns>
    Task<bool> VerifyAsync(KeyMove<TKey> move, CancellationToken ct);
}