using Microsoft.EntityFrameworkCore;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;

namespace Shardis.Migration.EntityFrameworkCore.Verification;

/// <summary>
/// Verification strategy computing canonical JSON + stable hash for minimal deterministic projection.
/// </summary>
/// <typeparam name="TKey">Underlying key type.</typeparam>
/// <typeparam name="TContext">DbContext type.</typeparam>
/// <typeparam name="TEntity">Entity type implementing <see cref="EntityFrameworkCore.IShardEntity{TKey}"/>.</typeparam>
public sealed class ChecksumVerificationStrategy<TKey, TContext, TEntity> : IVerificationStrategy<TKey>
    where TKey : notnull, IEquatable<TKey>
    where TContext : DbContext
    where TEntity : class, EntityFrameworkCore.IShardEntity<TKey>
{
    private readonly EntityFrameworkCore.IShardDbContextFactory<TContext> _factory;
    private readonly IStableCanonicalizer _canonicalizer;
    private readonly IStableHasher _hasher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChecksumVerificationStrategy{TKey, TContext, TEntity}"/> class.
    /// </summary>
    /// <param name="factory">Shard-scoped context factory.</param>
    /// <param name="canonicalizer">Canonical JSON serializer.</param>
    /// <param name="hasher">Stable hasher.</param>
    public ChecksumVerificationStrategy(EntityFrameworkCore.IShardDbContextFactory<TContext> factory, IStableCanonicalizer canonicalizer, IStableHasher hasher)
    {
        _factory = factory;
        _canonicalizer = canonicalizer;
        _hasher = hasher;
    }

    /// <summary>
    /// Loads the source and target entities and compares a stable hash of their canonical JSON representation.
    /// Returns false when either side is missing or when hashes differ.
    /// </summary>
    /// <param name="move">Key move being verified.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if hashes match; otherwise false.</returns>
    public async Task<bool> VerifyAsync(KeyMove<TKey> move, CancellationToken ct)
    {
        await using var sourceCtx = await _factory.CreateAsync(move.Source, ct).ConfigureAwait(false);
        await using var targetCtx = await _factory.CreateAsync(move.Target, ct).ConfigureAwait(false);

        var source = await sourceCtx.Set<TEntity>().FindAsync([move.Key.Value!], ct).ConfigureAwait(false);
        var target = await targetCtx.Set<TEntity>().FindAsync([move.Key.Value!], ct).ConfigureAwait(false);

        if (source is null || target is null)
        {
            return false;
        }

        // Minimal deterministic projection (by default entity itself). Users can override canonicalization via projection strategy if needed later.
        var sBytes = _canonicalizer.ToCanonicalUtf8(source);
        var tBytes = _canonicalizer.ToCanonicalUtf8(target);
        return _hasher.Hash(sBytes) == _hasher.Hash(tBytes);
    }
}