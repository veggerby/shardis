using Microsoft.EntityFrameworkCore;

using Shardis.Factories;
using Shardis.Model;

namespace Shardis.Query.EntityFrameworkCore.Factories;

/// <summary>
/// Basic EF Core shard factory creating <see cref="DbContext"/> instances per shard using options builder callback.
/// </summary>
/// <typeparam name="TContext">DbContext type.</typeparam>
public sealed class EntityFrameworkCoreShardFactory<TContext> : IShardFactory<TContext> where TContext : DbContext
{
    private readonly Func<ShardId, DbContextOptions<TContext>> _optionsFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityFrameworkCoreShardFactory{TContext}"/> class.
    /// </summary>
    /// <param name="optionsFactory">Factory producing configured <see cref="DbContextOptions{TContext}"/> for a shard.</param>
    public EntityFrameworkCoreShardFactory(Func<ShardId, DbContextOptions<TContext>> optionsFactory)
    {
        _optionsFactory = optionsFactory;
    }

    /// <inheritdoc />
    public TContext Create(ShardId shard)
    {
        var opts = _optionsFactory(shard);
        return (TContext)Activator.CreateInstance(typeof(TContext), opts)!;
    }

    /// <inheritdoc />
    public ValueTask<TContext> CreateAsync(ShardId shard, CancellationToken ct = default)
    {
        // Synchronous creation path; return as completed ValueTask to avoid allocation.
        return new ValueTask<TContext>(Create(shard));
    }
}

/// <summary>
/// Pooled EF Core shard factory leveraging <see cref="IDbContextFactory{TContext}"/> per shard for reduced allocations.
/// </summary>
/// <typeparam name="TContext">DbContext type.</typeparam>
public sealed class PooledEntityFrameworkCoreShardFactory<TContext> : IShardFactory<TContext> where TContext : DbContext
{
    private readonly IReadOnlyDictionary<ShardId, IDbContextFactory<TContext>> _perShard;

    /// <summary>
    /// Initializes a new instance of <see cref="PooledEntityFrameworkCoreShardFactory{TContext}"/>.
    /// </summary>
    /// <param name="perShard">Pre-created context factories keyed by shard.</param>
    public PooledEntityFrameworkCoreShardFactory(IReadOnlyDictionary<ShardId, IDbContextFactory<TContext>> perShard)
    {
        _perShard = perShard;
    }

    /// <inheritdoc />
    public TContext Create(ShardId shard) => _perShard[shard].CreateDbContext();

    /// <inheritdoc />
    public ValueTask<TContext> CreateAsync(ShardId shard, CancellationToken ct = default) => new(Create(shard));
}