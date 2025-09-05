namespace Shardis.Marten.Tests;

public sealed class Person
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}
