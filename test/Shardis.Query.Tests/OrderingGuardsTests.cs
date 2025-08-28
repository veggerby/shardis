using Shardis.Query.Execution;

namespace Shardis.Query.Tests;

public sealed class OrderingGuardsTests
{
    private sealed record Person(int Id);

    [Fact]
    public void OrderBy_Throws()
    {
        // arrange
        var exec = Substitute.For<IShardQueryExecutor>();
        var q = ShardQuery.For<Person>(exec);

        // act
        var ex = Assert.Throws<NotSupportedException>(() => q.OrderBy(p => p.Id));

        // assert
        ex.Message.Should().Contain("QueryAllShardsOrderedStreamingAsync");
    }
}