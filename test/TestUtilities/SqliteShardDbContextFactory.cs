using System.Collections.Concurrent;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Shardis.Migration.EntityFrameworkCore;
using Shardis.Model;

namespace Shardis.TestUtilities;

/// <summary>
/// Test-only Sqlite shard DbContext factory that provides per-factory isolated in-memory shared-cache databases
/// and persistent open connections so data survives across multiple DbContext instances within a test.
/// Not for production usage.
/// </summary>
/// <typeparam name="TContext">DbContext type.</typeparam>
public sealed class SqliteShardDbContextFactory<TContext> : IShardDbContextFactory<TContext>
    where TContext : DbContext
{
    private readonly ConcurrentDictionary<string, SqliteConnection> _connections = new();
    private readonly Func<ShardId, string> _nameSelector;
    private readonly Func<DbContextOptions<TContext>, TContext> _contextFactory;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    public SqliteShardDbContextFactory(Func<DbContextOptions<TContext>, TContext> contextFactory, Func<ShardId, string>? nameSelector = null)
    {
        _contextFactory = contextFactory;
        _nameSelector = nameSelector ?? (sid => sid.Value);
    }

    public Task<TContext> CreateAsync(ShardId shardId, CancellationToken cancellationToken = default)
    {
        var logicalName = _instanceId + "_" + _nameSelector(shardId);
        var conn = _connections.GetOrAdd(logicalName, static n =>
        {
            var c = new SqliteConnection($"DataSource=file:{n}?mode=memory&cache=shared");
            c.Open();
            return c;
        });

        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(conn)
            .EnableSensitiveDataLogging()
            .Options;

        var ctx = _contextFactory(options);
        ctx.Database.EnsureCreated();
        return Task.FromResult(ctx);
    }
}