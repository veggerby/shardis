using AwesomeAssertions;

using Marten;

using Shardis.Marten;
using Shardis.Model;
using Shardis.Querying.Linq;

using Xunit;

namespace Shardis.Marten.Tests;

public sealed class MartenQueryExecutorTests
{
    [PostgresFact]
    public async Task Marten_WhereSelect_Stream()
    {
        // arrange
        var conn = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
        {
            return; // skipped via PostgresFact (ensures env var) but guard defensively
        }
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
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

        // act
        var query = exec.Execute<Person>(readSession, q => q.Where(p => p.Age > 30).Select(p => p));
        var list = new List<Person>();
        await foreach (var p in query)
        {
            list.Add(p);
        }

        // assert
        list.Should().HaveCount(1);
        list[0].Name.Should().Be("Alice");
    }
}