using Shardis.Health;
using Shardis.Model;

namespace Shardis.Tests.Health;

public sealed class NoOpShardHealthPolicyTests
{
    [Fact]
    public async Task GetHealthAsync_Returns_Healthy_Status()
    {
        // arrange
        var policy = NoOpShardHealthPolicy.Instance;
        var shardId = new ShardId("shard-1");

        // act
        var report = await policy.GetHealthAsync(shardId);

        // assert
        report.ShardId.Should().Be(shardId);
        report.Status.Should().Be(ShardHealthStatus.Healthy);
        report.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAllHealthAsync_Returns_Empty()
    {
        // arrange
        var policy = NoOpShardHealthPolicy.Instance;

        // act
        var reports = new List<ShardHealthReport>();
        await foreach (var report in policy.GetAllHealthAsync())
        {
            reports.Add(report);
        }

        // assert
        reports.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordSuccess_Does_Not_Throw()
    {
        // arrange
        var policy = NoOpShardHealthPolicy.Instance;
        var shardId = new ShardId("shard-1");

        // act/assert
        await policy.RecordSuccessAsync(shardId);
    }

    [Fact]
    public async Task RecordFailure_Does_Not_Throw()
    {
        // arrange
        var policy = NoOpShardHealthPolicy.Instance;
        var shardId = new ShardId("shard-1");
        var exception = new InvalidOperationException("test");

        // act/assert
        await policy.RecordFailureAsync(shardId, exception);
    }

    [Fact]
    public async Task ProbeAsync_Returns_Healthy_Status()
    {
        // arrange
        var policy = NoOpShardHealthPolicy.Instance;
        var shardId = new ShardId("shard-1");

        // act
        var report = await policy.ProbeAsync(shardId);

        // assert
        report.ShardId.Should().Be(shardId);
        report.Status.Should().Be(ShardHealthStatus.Healthy);
    }
}
