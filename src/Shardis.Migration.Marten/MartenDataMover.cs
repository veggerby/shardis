namespace Shardis.Migration.Marten;

using global::Marten;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;
using Shardis.Model;

/// <summary>
/// Marten implementation of <see cref="IShardDataMover{TKey}"/> performing per-key copy and verify operations.
/// Uses deterministic canonicalization + hashing for verification; no rowversion support.
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
public sealed class MartenDataMover<TKey>(
    IMartenSessionFactory sessionFactory,
    IEntityProjectionStrategy projection,
    IStableCanonicalizer canonicalizer,
    IStableHasher hasher) : IShardDataMover<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly IMartenSessionFactory _sessionFactory = sessionFactory;
    private readonly IEntityProjectionStrategy _projection = projection;
    private readonly IStableCanonicalizer _canonicalizer = canonicalizer;
    private readonly IStableHasher _hasher = hasher;

    /// <summary>
    /// Copies the document for the specified key from the source shard to the target shard.
    /// No-op if the source document doesn't exist.
    /// </summary>
    /// <param name="move">Key move (source/target + key).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task CopyAsync(KeyMove<TKey> move, CancellationToken ct)
    {
        await using var sourceQuery = await _sessionFactory.CreateQuerySessionAsync(move.Source, ct).ConfigureAwait(false);
        var sourceDoc = await sourceQuery.LoadAsync<object>(move.Key.Value!, ct).ConfigureAwait(false);
        if (sourceDoc is null)
        {
            return; // nothing to copy
        }

        var projected = _projection.Project<object, object>(sourceDoc, new ProjectionContext(null, null));

        await using var targetSession = await _sessionFactory.CreateDocumentSessionAsync(move.Target, ct).ConfigureAwait(false);
        targetSession.Store(projected);
        await targetSession.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies the target shard document matches the source shard via canonical JSON hash comparison.
    /// </summary>
    /// <param name="move">Key move to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if hashes match; false otherwise.</returns>
    public async Task<bool> VerifyAsync(KeyMove<TKey> move, CancellationToken ct)
    {
        await using var sourceQuery = await _sessionFactory.CreateQuerySessionAsync(move.Source, ct).ConfigureAwait(false);
        await using var targetQuery = await _sessionFactory.CreateQuerySessionAsync(move.Target, ct).ConfigureAwait(false);

        var sourceDoc = await sourceQuery.LoadAsync<object>(move.Key.Value!, ct).ConfigureAwait(false);
        if (sourceDoc is null)
        {
            return false;
        }

        var targetDoc = await targetQuery.LoadAsync<object>(move.Key.Value!, ct).ConfigureAwait(false);
        if (targetDoc is null)
        {
            return false;
        }

        var sourceProj = _projection.Project<object, object>(sourceDoc, new ProjectionContext(null, null));
        var targetProj = _projection.Project<object, object>(targetDoc, new ProjectionContext(null, null));

        var sourceBytes = _canonicalizer.ToCanonicalUtf8(sourceProj);
        var targetBytes = _canonicalizer.ToCanonicalUtf8(targetProj);

        var sourceHash = _hasher.Hash(sourceBytes);
        var targetHash = _hasher.Hash(targetBytes);

        return sourceHash == targetHash;
    }
}