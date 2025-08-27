using System.Linq.Expressions;

using Shardis.Query.Execution;

namespace Shardis.Query.Tests;

public sealed class QueryModelBuilderTests
{
    private sealed record Person(int Id, string Name, int Age);

    [Fact]
    public void WhereSelect_ChainsCreateImmutableModel()
    {
        var exec = Substitute.For<IShardQueryExecutor>();
        var root = ShardQuery.For<Person>(exec);
        Expression<Func<Person, bool>> w1 = p => p.Age > 10;
        Expression<Func<Person, bool>> w2 = p => p.Name.StartsWith("A");
        Expression<Func<Person, int>> sel = p => p.Age;

        var q = root.Where(w1).Where(w2).Select(sel);

        root.Model.Where.Should().BeEmpty();
        q.Model.Where.Should().HaveCount(2);
        q.Model.Select.Should().Be(sel);
    }
}