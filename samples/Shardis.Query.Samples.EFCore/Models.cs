using Microsoft.EntityFrameworkCore;

namespace Shardis.Query.Samples.EFCore;

public sealed class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public sealed class PersonContext(DbContextOptions<PersonContext> options, string dbPath) : DbContext(options)
{
    private readonly string _dbPath = dbPath;

    public DbSet<Person> People => Set<Person>();
}