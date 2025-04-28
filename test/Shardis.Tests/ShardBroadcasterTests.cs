using NSubstitute;
using FluentAssertions;
using Shardis.Model;
using Shardis.Routing;

namespace Shardis.Tests;

public class ShardBroadcasterTests
{
    [Fact]
    public async Task QueryAllShardsAsync_ShouldAggregateResultsFromAllShards()
    {
        // Arrange
        var shard1 = Substitute.For<IShard<string>>();
        var shard2 = Substitute.For<IShard<string>>();

        shard1.CreateSession().Returns("Session1");
        shard2.CreateSession().Returns("Session2");

        var shards = new List<IShard<string>> { shard1, shard2 };
        var broadcaster = new ShardBroadcaster<IShard<string>, string>(shards);

        Func<string, Task<IEnumerable<string>>> query = session => Task.FromResult(new[] { session + "-Result" }.AsEnumerable());

        // Act
        var results = await broadcaster.QueryAllShardsAsync(query);

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain("Session1-Result");
        results.Should().Contain("Session2-Result");
    }

    [Fact]
    public async Task QueryAllShardsAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var shard = Substitute.For<IShard<string>>();
        var shards = new List<IShard<string>> { shard };
        var broadcaster = new ShardBroadcaster<IShard<string>, string>(shards);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => broadcaster.QueryAllShardsAsync<string>(null!));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenShardsIsNull()
    {
        // Act & Assert
        Action act = () => new ShardBroadcaster<IShard<string>, string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}