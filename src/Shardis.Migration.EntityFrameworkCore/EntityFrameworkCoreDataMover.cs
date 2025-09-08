using Microsoft.EntityFrameworkCore;

using Shardis.Migration.Abstractions;
using Shardis.Migration.Model;
using Shardis.Model;

namespace Shardis.Migration.EntityFrameworkCore;

/// <summary>
/// Default EF Core implementation of <see cref="IShardDataMover{TKey}"/> that performs per-key copy (and optional
/// lightweight verification) between source and target shards using an injected <see cref="IShardDbContextFactory{TContext}"/>.
/// The mover intentionally avoids provider-specific bulk merge SQL to remain deterministic and portable; advanced
/// scenarios can register a custom mover with optimized set-based operations.
/// </summary>
/// <typeparam name="TKey">Underlying shard key type.</typeparam>
/// <typeparam name="TContext">Concrete DbContext type.</typeparam>
/// <typeparam name="TEntity">Entity type implementing <see cref="IShardEntity{TKey}"/>.</typeparam>
/// <remarks>
/// Thread safety: a single instance is safe for concurrent use because per-operation state is confined to short-lived
/// DbContext instances created via the injected factory.
/// </remarks>
public sealed class EntityFrameworkCoreDataMover<TKey, TContext, TEntity> : IShardDataMover<TKey>
    where TKey : notnull, IEquatable<TKey>
    where TContext : DbContext
    where TEntity : class, IShardEntity<TKey>
{
    private readonly IShardDbContextFactory<TContext> _factory;
    private readonly IEntityProjectionStrategy _projection;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkCoreDataMover{TKey, TContext, TEntity}"/> class.
    /// </summary>
    /// <param name="factory">Shard-scoped context factory.</param>
    /// <param name="projection">Projection strategy (identity by default).</param>
    public EntityFrameworkCoreDataMover(IShardDbContextFactory<TContext> factory, IEntityProjectionStrategy projection)
    {
        _factory = factory;
        _projection = projection;
    }

    /// <summary>
    /// Copies the entity for <paramref name="move"/> from the source shard to the target shard.
    /// Performs a best-effort lightweight upsert (raw SQL for relational providers) then falls back to tracked update.
    /// No-op when the source entity is absent.
    /// </summary>
    /// <param name="move">Key move describing source/target.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task CopyAsync(KeyMove<TKey> move, CancellationToken ct)
    {
        // Load source entity (no tracking to avoid unnecessary change tracker overhead)
        await using var sourceCtx = await _factory.CreateAsync(move.Source, ct).ConfigureAwait(false);
        var set = sourceCtx.Set<TEntity>();
        var entity = await set.FindAsync([move.Key.Value!], ct).ConfigureAwait(false);
        if (entity is null)
        {
            return; // nothing to copy
        }

        // Project (identity for default)
        var projected = _projection.Project<TEntity, TEntity>(entity, new ProjectionContext(null, null));

        // Upsert into target
        await using var targetCtx = await _factory.CreateAsync(move.Target, ct).ConfigureAwait(false);
        var targetSet = targetCtx.Set<TEntity>();

        // NOTE: Raw SQL upsert intentionally omitted for test reliability and portability.
        // Advanced users can supply a custom mover with provider-optimized set-based operations.

        // Attach/Update entity state (ensures full column copy)
        var existing = await targetSet.FindAsync([move.Key.Value!], ct).ConfigureAwait(false);
        if (existing is null)
        {
            await targetSet.AddAsync(projected, ct).ConfigureAwait(false);
        }
        else
        {
            targetCtx.Entry(existing).CurrentValues.SetValues(projected);
        }

        await targetCtx.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies the previously copied entity. RowVersion equality is preferred when both sides expose a value;
    /// otherwise a weak success (true) is returned when both entities exist. Consumers wanting stronger
    /// guarantees should replace this mover or use a checksum verification strategy.
    /// </summary>
    /// <param name="move">Key move to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if verification passed; otherwise false.</returns>
    public async Task<bool> VerifyAsync(KeyMove<TKey> move, CancellationToken ct)
    {
        await using var sourceCtx = await _factory.CreateAsync(move.Source, ct).ConfigureAwait(false);
        await using var targetCtx = await _factory.CreateAsync(move.Target, ct).ConfigureAwait(false);

        var source = await sourceCtx.Set<TEntity>().FindAsync([move.Key.Value!], ct).ConfigureAwait(false);
        var target = await targetCtx.Set<TEntity>().FindAsync([move.Key.Value!], ct).ConfigureAwait(false);
        if (source is null || target is null)
        {
            return false; // mismatch or missing
        }
        if (source.RowVersion is not null && target.RowVersion is not null)
        {
            return source.RowVersion.AsSpan().SequenceEqual(target.RowVersion);
        }
        // Fallback: basic property equality on key only (weak) - encourage checksum strategy for full correctness.
        return true;
    }
}