using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using Shardis.Migration.EntityFrameworkCore;
using Shardis.Model;

namespace Shardis.Migration.Durable.Sample;

public sealed class UserProfile : IShardEntity<string>
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public byte[]? RowVersion { get; set; }
    public string Key => Id;
}

public sealed class UserProfileContext(string tableName, DbContextOptions<UserProfileContext> options) : DbContext(options)
{
    public string TableName { get; } = tableName;
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Map entity to provided table with explicit lower-case column names.
        modelBuilder.Entity<UserProfile>(b =>
        {
            b.ToTable(TableName);
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Name).HasColumnName("name");
            b.Property(x => x.Age).HasColumnName("age");
            b.Property(x => x.RowVersion).HasColumnName("rowversion").IsRowVersion();
        });
    }
}

public sealed class UserProfileContextFactory : IShardDbContextFactory<UserProfileContext>
{
    private readonly IServiceProvider _root;
    public UserProfileContextFactory(IServiceProvider root) => _root = root;

    public Task<UserProfileContext> CreateAsync(ShardId shard, CancellationToken ct)
    {
        var table = shard.Value switch
        {
            "source" => "user_profiles_source",
            "target" => "user_profiles_target",
            _ => "user_profiles"
        };
        var builder = new DbContextOptionsBuilder<UserProfileContext>();
        // We rely on the same connection string each time; obtain from Runner.Config
        var cfg = _root.GetRequiredService<Runner.Config>();
        builder.UseNpgsql(cfg.ConnectionString, o => o.UseRelationalNulls());
        // Ensure model cache key varies per table name so each shard has isolated model mapping.
        builder.ReplaceService<IModelCacheKeyFactory, TableNameModelCacheKeyFactory>();
        // Disable tracking / change detection overhead globally (per-use contexts)
        builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        var ctx = new UserProfileContext(table, builder.Options);
        return Task.FromResult(ctx);
    }
}

internal sealed class TableNameModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        if (context is UserProfileContext up)
        {
            return (context.GetType(), up.TableName, designTime);
        }
        return (context.GetType(), designTime);
    }
}