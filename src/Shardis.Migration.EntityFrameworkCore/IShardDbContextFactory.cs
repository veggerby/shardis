using Microsoft.EntityFrameworkCore;

using Shardis.Model;

namespace Shardis.Migration.EntityFrameworkCore;

/// <summary>
/// Factory for creating a <see cref="DbContext"/> bound to a specific <see cref="ShardId"/>.
/// Implementations should configure connection string / schema per shard.
/// </summary>
public interface IShardDbContextFactory<TContext>
    where TContext : DbContext
{
    /// <summary>Creates a new <typeparamref name="TContext"/> for a shard. Caller owns disposal.</summary>
    /// <param name="shardId">Target shard identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Context instance.</returns>
    Task<TContext> CreateAsync(ShardId shardId, CancellationToken cancellationToken = default);
}