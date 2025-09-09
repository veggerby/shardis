using Microsoft.EntityFrameworkCore;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;

namespace Shardis.Migration.EntityFrameworkCore.Verification;

/// <summary>
/// Verification strategy that performs a strict binary comparison of RowVersion (timestamp) properties between
/// source and target entities. Requires both entities to exist and expose non-null RowVersion values. Provides a
/// fast check suitable for optimistic concurrency enabled schemas.
/// </summary>
/// <typeparam name="TKey">Underlying key type.</typeparam>
/// <typeparam name="TContext">DbContext type.</typeparam>
/// <typeparam name="TEntity">Entity type implementing <see cref="EntityFrameworkCore.IShardEntity{TKey}"/>.</typeparam>
/// <remarks>Thread safety: stateless aside from the injected factory; safe for concurrent use.</remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="RowVersionVerificationStrategy{TKey, TContext, TEntity}"/> class.
/// </remarks>
/// <param name="factory">Shard-scoped context factory.</param>
public sealed class RowVersionVerificationStrategy<TKey, TContext, TEntity>(EntityFrameworkCore.IShardDbContextFactory<TContext> factory) : IVerificationStrategy<TKey>
    where TKey : notnull, IEquatable<TKey>
    where TContext : DbContext
    where TEntity : class, EntityFrameworkCore.IShardEntity<TKey>
{
    private readonly EntityFrameworkCore.IShardDbContextFactory<TContext> _factory = factory;

    /// <summary>
    /// Compares the binary RowVersion values for the entity on source and target shards. All conditions must hold:
    /// both entities exist and both expose non-null rowversion values that are byte-wise equal.
    /// </summary>
    /// <param name="move">Key move being verified.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if entities exist and rowversions match; otherwise false.</returns>
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
        if (source.RowVersion is null || target.RowVersion is null)
        {
            return false; // require both rowversions for certainty
        }
        return source.RowVersion.AsSpan().SequenceEqual(target.RowVersion);
    }
}