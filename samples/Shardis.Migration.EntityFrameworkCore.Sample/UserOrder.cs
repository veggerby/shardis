using Microsoft.EntityFrameworkCore;

using Shardis.Migration.EntityFrameworkCore;
using Shardis.Model;

namespace Shardis.Migration.EFCore.Sample;

public sealed class UserOrder : IShardEntity<string>
{
    public string Id { get; set; } = string.Empty; // key format: order-{n:000000}
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedUtc { get; set; }

    // IShardEntity implementation (read-only projection)
    public string Key => Id;
    public byte[]? RowVersion { get; private set; }
}

public sealed class OrdersContext(DbContextOptions<OrdersContext> options) : DbContext(options)
{
    public DbSet<UserOrder> Orders => Set<UserOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserOrder>(b =>
        {
            b.ToTable("user_orders");
            b.HasKey(x => x.Id);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.Property(x => x.UserId).IsRequired();
            b.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            b.Property(x => x.CreatedUtc).IsRequired();
        });
    }
}

public sealed class OrdersContextFactory : IShardDbContextFactory<OrdersContext>
{
    private readonly string _host;
    private readonly string _port;
    private readonly string _user;
    private readonly string _pw;

    public OrdersContextFactory()
    {
        _host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        _port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        _user = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
        _pw   = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "postgres";
    }

    private string Build(string db) => $"Host={_host};Port={_port};Username={_user};Password={_pw};Database={db}";

    public Task<OrdersContext> CreateAsync(ShardId shardId, CancellationToken cancellationToken = default)
    {
        var db = $"orders_shard_{shardId.Value}"; // database per shard id
        var options = new DbContextOptionsBuilder<OrdersContext>()
            .UseNpgsql(Build(db))
            .Options;
        return Task.FromResult(new OrdersContext(options));
    }
}
