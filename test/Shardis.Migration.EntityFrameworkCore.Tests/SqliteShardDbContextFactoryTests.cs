using Microsoft.EntityFrameworkCore;

using Shardis.Model;
using Shardis.TestUtilities;

namespace Shardis.Migration.EntityFrameworkCore.Tests;

public class SqliteShardDbContextFactoryTests
{
    private sealed class DummyEntity : IShardEntity<int>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte[]? RowVersion { get; set; }
        public int Key => Id;
    }

    private sealed class DummyContext(DbContextOptions<DummyContext> options) : DbContext(options)
    {
        public DbSet<DummyEntity> Entities => Set<DummyEntity>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DummyEntity>().HasKey(e => e.Id);
        }
    }

    [Fact]
    public async Task CreatesIsolatedDatabasesPerFactoryInstance()
    {
        // arrange
        var shardA = new ShardId("sA");
        var shardB = new ShardId("sB");
        var factory1 = new SqliteShardDbContextFactory<DummyContext>(opts => new DummyContext(opts));
        var factory2 = new SqliteShardDbContextFactory<DummyContext>(opts => new DummyContext(opts));

        // act
        using (var c1a = await factory1.CreateAsync(shardA))
        {
            c1a.Entities.Add(new DummyEntity { Id = 1, Name = "F1-A" });
            c1a.SaveChanges();
        }
        using (var c2a = await factory2.CreateAsync(shardA))
        {
            // assert isolation: shardA in factory2 should be empty
            c2a.Entities.Any().Should().BeFalse();
        }
    }

    [Fact]
    public async Task PersistsDataAcrossContextInstancesWithinFactory()
    {
        // arrange
        var shard = new ShardId("persist");
        var factory = new SqliteShardDbContextFactory<DummyContext>(opts => new DummyContext(opts));

        // act
        using (var w = await factory.CreateAsync(shard))
        {
            w.Entities.Add(new DummyEntity { Id = 7, Name = "Seven" });
            w.SaveChanges();
        }
        using (var r = await factory.CreateAsync(shard))
        {
            var e = await r.Entities.FindAsync(7);
            e.Should().NotBeNull();
            e!.Name.Should().Be("Seven");
        }
    }
}