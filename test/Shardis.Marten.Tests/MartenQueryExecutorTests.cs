using AwesomeAssertions;

using Marten;

using Shardis.Model;
using Shardis.Querying.Linq;

using Xunit;

namespace Shardis.Marten.Tests;

[Trait("Category", "Integration")]
public sealed class MartenQueryExecutorTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public MartenQueryExecutorTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task Marten_WhereSelect_Stream()
    {
        // arrange
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(_fixture.ConnectionString);
        });
        var shard = new MartenShard(new ShardId("0"), store);
        var uniquePrefix = $"T_{Guid.NewGuid():N}";
        using (var session = shard.CreateSession())
        {
            session.Store(new Person { Id = Guid.NewGuid(), Name = uniquePrefix + "_Alice", Age = 34 });
            session.Store(new Person { Id = Guid.NewGuid(), Name = uniquePrefix + "_Bob", Age = 25 });
            await session.SaveChangesAsync();
        }
        using var readSession = shard.CreateSession();
        var exec = shard.QueryExecutor;

        // act
        // Narrow query to the uniquely inserted person to avoid interference from pre-existing seeded data
        var query = exec.Execute<Person>(readSession, q => q.Where(p => p.Name.StartsWith(uniquePrefix)).Select(p => p));
        var list = new List<Person>();
        await foreach (var p in query)
        {
            list.Add(p);
        }

        // assert
        list.Should().HaveCount(2);
        list.Should().Contain(p => p.Name.EndsWith("_Alice") && p.Age == 34);
        list.Should().Contain(p => p.Name.EndsWith("_Bob") && p.Age == 25);
    }
}