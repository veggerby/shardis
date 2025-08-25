using Shardis.Model;
using Shardis.Querying;

namespace Shardis.Tests;

public class ShardBroadcasterTests
{
    [Fact]
    public async Task QueryAllShardsAsync_ShouldAggregateResultsFromAllShards()
    {
        // arrange
        var shard1 = Substitute.For<IShard<string>>();
        var shard2 = Substitute.For<IShard<string>>();

        shard1.CreateSession().Returns("Session1");
        shard2.CreateSession().Returns("Session2");

        var shards = new List<IShard<string>> { shard1, shard2 };
        var broadcaster = new ShardBroadcaster<IShard<string>, string>(shards);

        Func<string, Task<IEnumerable<string>>> query = session => Task.FromResult(new[] { session + "-Result" }.AsEnumerable());

        // act
        var results = await broadcaster.QueryAllShardsAsync(query);

        // assert
        results.Should().HaveCount(2);
        results.Should().Contain("Session1-Result");
        results.Should().Contain("Session2-Result");
    }

    [Fact]
    public async Task QueryAllShardsAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // arrange
        var shard = Substitute.For<IShard<string>>();
        var shards = new List<IShard<string>> { shard };
        var broadcaster = new ShardBroadcaster<IShard<string>, string>(shards);

        // act & assert
        Func<Task> invoke = () => broadcaster.QueryAllShardsAsync<string>(null!);
        var ex = await invoke.Should().ThrowAsync<ArgumentNullException>();
        ex.Which.ParamName.Should().Be("query");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenShardsIsNull()
    {
        // act & assert
        Action construct = () => new ShardBroadcaster<IShard<string>, string>(null!);
        var ex = construct.Should().Throw<ArgumentNullException>();
        ex.Which.ParamName.Should().Be("shards");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenNoShards()
    {
        // act
        Action act = () => new ShardBroadcaster<IShard<string>, string>(Array.Empty<IShard<string>>());

        // assert
        act.Should().Throw<ArgumentException>();
    }
}