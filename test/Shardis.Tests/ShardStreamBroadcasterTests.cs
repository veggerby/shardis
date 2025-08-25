using System.Diagnostics;

using Shardis.Model;
using Shardis.Querying;
using Shardis.Tests.TestHelpers;

namespace Shardis.Tests;

public class ShardStreamBroadcasterTests
{
    [Fact]
    public async Task QueryAllShardsAsync_ShouldAggregateResultsFromAllShards()
    {
        // arrange
        var shard1 = new TestShard<string>("Shard1", "Session1");
        var shard2 = new TestShard<string>("Shard2", "Session2");

        var shards = new List<IShard<string>> { shard1, shard2 };
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards);

        Func<string, IAsyncEnumerable<string>> query = session => GetMockedResults(session);

        // act
        var results = new List<string>();
        await foreach (var result in broadcaster.QueryAllShardsAsync(query))
        {
            results.Add(result.Item);
        }

        // assert
        results.Should().BeEquivalentTo(new[]
                {
            "Session1-Result1",
            "Session1-Result2",
            "Session2-Result1",
            "Session2-Result2"
        });
    }

    [Fact]
    public async Task QueryAllShardsAsync_ShouldThrowArgumentNullException_WhenQueryIsNull()
    {
        // arrange
        var shard = new TestShard<string>("Shard1", "Session1");
        var shards = new List<IShard<string>> { shard };
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards);

        // act
        Func<Task> invoke = async () =>
        {
            await foreach (var _ in broadcaster.QueryAllShardsAsync<string>(null!)) { }
        };
        var ex = await invoke.Should().ThrowAsync<ArgumentNullException>();
        ex.Which.ParamName.Should().Be("query");

        // assert (exception type asserted above)
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenShardsIsNull()
    {
        // arrange / act / assert
        Action construct = () => new ShardStreamBroadcaster<IShard<string>, string>(null!);
        var ex = construct.Should().Throw<ArgumentNullException>();
        ex.Which.ParamName.Should().Be("shards");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenNoShards()
    {
        // act
        Action act = () => new ShardStreamBroadcaster<IShard<string>, string>(Array.Empty<IShard<string>>());

        // assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task QueryAllShardsAsync_ShouldYieldFastShardResultsBeforeSlowShards()
    {
        // arrange
        var fastShard = new TestShard<string>("ShardFast", "FastSession");
        var slowShard = new TestShard<string>("ShardSlow", "SlowSession");

        var shards = new List<IShard<string>> { fastShard, slowShard };
        var broadcaster = new ShardStreamBroadcaster<IShard<string>, string>(shards);

        async IAsyncEnumerable<string> FastQuery(string session)
        {
            yield return "FastResult1";
            await Task.Delay(10);
            yield return "FastResult2";
        }

        async IAsyncEnumerable<string> SlowQuery(string session)
        {
            await Task.Delay(500);
            yield return "SlowResult1";
            await Task.Delay(10);
            yield return "SlowResult2";
        }

        Func<string, IAsyncEnumerable<string>> query = session =>
            session == "FastSession" ? FastQuery(session) : SlowQuery(session);

        var yielded = new List<(string Result, long Timestamp)>();
        var stopwatch = Stopwatch.StartNew();

        // act
        await foreach (var result in broadcaster.QueryAllShardsAsync(query))
        {
            yielded.Add((result.Item, stopwatch.ElapsedMilliseconds));
        }

        stopwatch.Stop();

        // assert
        yielded.Should().Contain(r => r.Result.StartsWith("Fast"));
        yielded.Should().Contain(r => r.Result.StartsWith("Slow"));

        var firstSlow = yielded.First(r => r.Result.StartsWith("Slow"));
        var lastFast = yielded.Last(r => r.Result.StartsWith("Fast"));

        lastFast.Timestamp.Should().BeLessThan(firstSlow.Timestamp);
    }

    private async IAsyncEnumerable<string> GetMockedResults(string session)
    {
        yield return $"{session}-Result1";
        await Task.Yield();
        yield return $"{session}-Result2";
    }
}