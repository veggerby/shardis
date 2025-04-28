using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Shardis.Model;
using Shardis.Routing;
using Xunit;

namespace Shardis.Tests;

public class ShardStreamBroadcasterTests
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
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards);

        Func<string, IAsyncEnumerable<string>> query = session => GetMockedResults(session);

        // Act
        var results = new List<string>();
        await foreach (var result in broadcaster.QueryAllShardsAsync(query))
        {
            results.Add(result);
        }

        // Assert
        results.Should().HaveCount(4);
        results.Should().Contain("Session1-Result1");
        results.Should().Contain("Session1-Result2");
        results.Should().Contain("Session2-Result1");
        results.Should().Contain("Session2-Result2");
    }

    private async IAsyncEnumerable<string> GetMockedResults(string session)
    {
        yield return await Task.FromResult(session + "-Result1");
        yield return await Task.FromResult(session + "-Result2");
    }

    [Fact]
    public async Task QueryAllShardsAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // Arrange
        var shard = Substitute.For<IShard<string>>();
        var shards = new List<IShard<string>> { shard };
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards);

        // Act
        var asyncEnumerable = broadcaster.QueryAllShardsAsync<string>(null!);

        // Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in asyncEnumerable)
            {
                // Should not reach here
            }
        });

        Assert.Equal("query", exception.ParamName);
    }


    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenShardsIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ShardStreamBroadcaster<IShard<string>, string>(null!));
    }

    [Fact]
    public async Task QueryAllShardsAsync_ShouldYieldFastShardResultsBeforeSlowShards()
    {
        // Arrange
        var fastShard = Substitute.For<IShard<string>>();
        var slowShard = Substitute.For<IShard<string>>();

        fastShard.CreateSession().Returns("FastSession");
        slowShard.CreateSession().Returns("SlowSession");

        var shards = new List<IShard<string>> { fastShard, slowShard };
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards);

        async IAsyncEnumerable<string> FastShardQuery(string session)
        {
            yield return "FastResult1";
            await Task.Delay(10); // Small delay to simulate async behavior
            yield return "FastResult2";
        }

        async IAsyncEnumerable<string> SlowShardQuery(string session)
        {
            await Task.Delay(500); // Simulate slow shard
            yield return "SlowResult1";
            await Task.Delay(10);
            yield return "SlowResult2";
        }

        Func<string, IAsyncEnumerable<string>> query = session =>
            session == "FastSession" ? FastShardQuery(session) : SlowShardQuery(session);

        var yieldedResults = new List<(string Value, long Timestamp)>();
        var stopwatch = Stopwatch.StartNew();

        // Act
        await foreach (var result in broadcaster.QueryAllShardsAsync(query))
        {
            yieldedResults.Add((result, stopwatch.ElapsedMilliseconds));
        }

        stopwatch.Stop();

        // Assert
        yieldedResults.Should().NotBeEmpty();
        yieldedResults.Should().Contain(r => r.Value.StartsWith("Fast"));
        yieldedResults.Should().Contain(r => r.Value.StartsWith("Slow"));

        var firstSlowResult = yieldedResults.First(r => r.Value.StartsWith("Slow"));
        var lastFastResult = yieldedResults.Last(r => r.Value.StartsWith("Fast"));

        // Assert that at least one FastResult arrived before any SlowResult
        lastFastResult.Timestamp.Should().BeLessThan(firstSlowResult.Timestamp);
    }
}