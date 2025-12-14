using Microsoft.EntityFrameworkCore;

namespace Shardis.Health.Sample;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int ShardId { get; set; }
}

public class ProductContext : DbContext
{
    public ProductContext(DbContextOptions<ProductContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
}
