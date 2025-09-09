namespace Shardis.Migration.Marten.Verification;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Marten;
using Shardis.Migration.Model;

/// <summary>
/// Verifies documents across shards by computing canonical JSON hash for a projected document shape.
/// </summary>
public sealed class DocumentChecksumVerificationStrategy<TKey>(
    IMartenSessionFactory sessionFactory,
    IEntityProjectionStrategy projection,
    IStableCanonicalizer canonicalizer,
    IStableHasher hasher) : IVerificationStrategy<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly IMartenSessionFactory _sessionFactory = sessionFactory;
    private readonly IEntityProjectionStrategy _projection = projection;
    private readonly IStableCanonicalizer _canonicalizer = canonicalizer;
    private readonly IStableHasher _hasher = hasher;

    /// <summary>
    /// Verifies the migrated document matches between source and target shards via canonical hash.
    /// </summary>
    /// <param name="move">Key move to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<bool> VerifyAsync(KeyMove<TKey> move, CancellationToken cancellationToken)
    {
        await using var sourceQuery = await _sessionFactory.CreateQuerySessionAsync(move.Source, cancellationToken);
        await using var targetQuery = await _sessionFactory.CreateQuerySessionAsync(move.Target, cancellationToken);

        var sourceDoc = await sourceQuery.LoadAsync<object>(move.Key.Value!, cancellationToken);
        if (sourceDoc is null)
        {
            return false;
        }

        var targetDoc = await targetQuery.LoadAsync<object>(move.Key.Value!, cancellationToken);
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