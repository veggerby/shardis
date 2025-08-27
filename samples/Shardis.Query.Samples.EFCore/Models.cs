using Microsoft.EntityFrameworkCore;

namespace Shardis.Query.Samples.EFCore;

public sealed class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public sealed class PersonContext : DbContext
{
    private readonly string _dbPath;
    public PersonContext(DbContextOptions<PersonContext> options, string dbPath) : base(options)
    {
        _dbPath = dbPath;
    }
    public DbSet<Person> People => Set<Person>();
}