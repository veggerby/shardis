using Marten;
using Shardis.Marten;
using Shardis.Model;
using Shardis.Querying.Linq;
using Xunit;
using AwesomeAssertions;

namespace Shardis.Marten.Tests;

public sealed class MartenQueryExecutorTests
{
    [Fact]
    public async Task Marten_WhereSelect_Stream()
    {
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection("host=localhost;database=shardis_test;password=pass;username=postgres");
        });
    var shard = new MartenShard(new ShardId("0"), store);
        using (var session = shard.CreateSession())
        {
            session.Store(new Person { Id = Guid.NewGuid(), Name = "Alice", Age = 34 });
            session.Store(new Person { Id = Guid.NewGuid(), Name = "Bob", Age = 25 });
            await session.SaveChangesAsync();
        }

        using var readSession = shard.CreateSession();
        var exec = shard.QueryExecutor;
    var query = exec.Execute<Person>(readSession, q => q.Where(p => p.Age > 30).Select(p => p));
        var list = new List<Person>();
        await foreach (var p in query)
        {
            list.Add(p);
        }
        list.Should().HaveCount(1);
        list[0].Name.Should().Be("Alice");
    }

    private sealed class Person { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; public int Age { get; set; } }
}
