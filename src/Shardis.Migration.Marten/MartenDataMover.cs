namespace Shardis.Migration.Marten;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;

/// <summary>
/// Marten implementation of <see cref="IShardDataMover{TKey}"/> performing per-key copy operations only.
/// Verification is delegated to a registered <see cref="IVerificationStrategy{TKey}"/> (e.g. <see cref="Verification.DocumentChecksumVerificationStrategy{TKey}"/>).
/// </summary>
/// <typeparam name="TKey">Key type.</typeparam>
public sealed class MartenDataMover<TKey>(
    IMartenSessionFactory sessionFactory,
    IEntityProjectionStrategy projection,
    IVerificationStrategy<TKey> verification) : IShardDataMover<TKey>
    where TKey : notnull, IEquatable<TKey>
{
    private readonly IMartenSessionFactory _sessionFactory = sessionFactory;
    private readonly IEntityProjectionStrategy _projection = projection;
    private readonly IVerificationStrategy<TKey> _verification = verification;

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
    /// Delegates verification to the configured <see cref="IVerificationStrategy{TKey}"/> to avoid duplication.
    /// </summary>
    public Task<bool> VerifyAsync(KeyMove<TKey> move, CancellationToken ct) => _verification.VerifyAsync(move, ct);

}