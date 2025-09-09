using AwesomeAssertions;

using Shardis.Logging;

using Xunit;

namespace Shardis.Tests.Logging;

public sealed class ShardisLoggerTests
{
    [Fact]
    public void NullLogger_IsDisabled_ForAllLevels()
    {
        // arrange

        // act
        var levels = Enum.GetValues(typeof(ShardisLogLevel)).Cast<ShardisLogLevel>();

        // assert
        foreach (var lvl in levels)
        {
            NullShardisLogger.Instance.IsEnabled(lvl).Should().BeFalse();
        }
    }

    [Fact]
    public void NullLogger_Log_DoesNotThrow()
    {
        // arrange

        // act & assert (no exception)
        NullShardisLogger.Instance.Log(ShardisLogLevel.Information, "msg");
        NullShardisLogger.Instance.Log(ShardisLogLevel.Error, "err", new InvalidOperationException("boom"));
    }

    [Fact]
    public void InMemoryLogger_Respects_MinLevel()
    {
        // arrange
        var logger = new InMemoryShardisLogger(ShardisLogLevel.Warning);

        // act
        // assert
        logger.IsEnabled(ShardisLogLevel.Trace).Should().BeFalse();
        logger.IsEnabled(ShardisLogLevel.Information).Should().BeFalse();
        logger.IsEnabled(ShardisLogLevel.Warning).Should().BeTrue();
        logger.IsEnabled(ShardisLogLevel.Error).Should().BeTrue();
    }

    [Fact]
    public void InMemoryLogger_Captures_Message_And_Exception()
    {
        // arrange
        var logger = new InMemoryShardisLogger(ShardisLogLevel.Trace);
        var ex = new InvalidOperationException("broken");

        // act
        logger.Log(ShardisLogLevel.Debug, "hello world");
        logger.Log(ShardisLogLevel.Error, "failure", ex);
        var entries = logger.Entries.ToList();

        // assert
        entries.Count.Should().Be(2);
        entries[0].Should().Contain("Debug: hello world");
        entries[1].Should().Contain("Error: failure | ex=InvalidOperationException: broken");
    }

    [Fact]
    public void InMemoryLogger_Filters_By_MinLevel()
    {
        // arrange
        var logger = new InMemoryShardisLogger(ShardisLogLevel.Error);

        // act
        logger.Log(ShardisLogLevel.Warning, "ignored");
        logger.Log(ShardisLogLevel.Error, "captured");
        var entries = logger.Entries.ToList();

        // assert
        entries.Count.Should().Be(1);
        entries[0].Should().Contain("Error: captured");
    }

    [Fact]
    public async Task InMemoryLogger_IsThreadSafe_ForConcurrentWrites()
    {
        // arrange
        var logger = new InMemoryShardisLogger(ShardisLogLevel.Trace);
        var tasks = new List<Task>();
        const int writers = 8;
        const int perWriter = 50;

        // act
        for (int i = 0; i < writers; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < perWriter; j++)
                {
                    logger.Log(ShardisLogLevel.Debug, $"w{idx}:{j}");
                }
            }));
        }
        await Task.WhenAll(tasks);
        var entries = logger.Entries;

        // assert
        entries.Count.Should().Be(writers * perWriter);
    }
}