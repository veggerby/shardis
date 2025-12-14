using Shardis.Health;
using Shardis.Model;

namespace Shardis.Tests.Health;

public sealed class PeriodicShardHealthPolicyTests
{
    private sealed class TestProbe : IShardHealthProbe
    {
        private readonly Func<ShardId, ValueTask<ShardHealthReport>> _probeFunc;

        public TestProbe(Func<ShardId, ValueTask<ShardHealthReport>> probeFunc)
        {
            _probeFunc = probeFunc;
        }

        public ValueTask<ShardHealthReport> ExecuteAsync(ShardId shardId, CancellationToken ct = default)
        {
            return _probeFunc(shardId);
        }
    }

    [Fact]
    public async Task GetHealthAsync_Returns_Unknown_Initially()
    {
        // arrange
        var probe = new TestProbe(_ => ValueTask.FromResult(new ShardHealthReport
        {
            ShardId = new ShardId("shard-1"),
            Status = ShardHealthStatus.Healthy,
            Timestamp = DateTimeOffset.UtcNow
        }));
        var options = new ShardHealthPolicyOptions { ProbeInterval = TimeSpan.Zero };
        var policy = new PeriodicShardHealthPolicy(probe, options);
        var shardId = new ShardId("shard-1");

        // act
        var report = await policy.GetHealthAsync(shardId);

        // assert
        report.ShardId.Should().Be(shardId);
        report.Status.Should().Be(ShardHealthStatus.Unknown);
    }

    [Fact]
    public async Task RecordSuccess_Transitions_To_Healthy()
    {
        // arrange
        var probe = new TestProbe(_ => ValueTask.FromResult(new ShardHealthReport
        {
            ShardId = new ShardId("shard-1"),
            Status = ShardHealthStatus.Healthy,
            Timestamp = DateTimeOffset.UtcNow
        }));
        var options = new ShardHealthPolicyOptions
        {
            ProbeInterval = TimeSpan.Zero,
            ReactiveTrackingEnabled = true
        };
        var policy = new PeriodicShardHealthPolicy(probe, options);
        var shardId = new ShardId("shard-1");

        // act
        await policy.RecordSuccessAsync(shardId);
        var report = await policy.GetHealthAsync(shardId);

        // assert
        report.Status.Should().Be(ShardHealthStatus.Healthy);
    }

    [Fact]
    public async Task RecordFailure_Exceeding_Threshold_Transitions_To_Unhealthy()
    {
        // arrange
        var probe = new TestProbe(_ => ValueTask.FromResult(new ShardHealthReport
        {
            ShardId = new ShardId("shard-1"),
            Status = ShardHealthStatus.Healthy,
            Timestamp = DateTimeOffset.UtcNow
        }));
        var options = new ShardHealthPolicyOptions
        {
            ProbeInterval = TimeSpan.Zero,
            UnhealthyThreshold = 3,
            ReactiveTrackingEnabled = true
        };
        var policy = new PeriodicShardHealthPolicy(probe, options);
        var shardId = new ShardId("shard-1");
        var exception = new InvalidOperationException("test failure");

        // act
        await policy.RecordFailureAsync(shardId, exception);
        await policy.RecordFailureAsync(shardId, exception);
        await policy.RecordFailureAsync(shardId, exception);
        var report = await policy.GetHealthAsync(shardId);

        // assert
        report.Status.Should().Be(ShardHealthStatus.Unhealthy);
        report.Exception.Should().Be(exception);
    }

    [Fact]
    public async Task RecordSuccess_After_Unhealthy_Recovers()
    {
        // arrange
        var probe = new TestProbe(_ => ValueTask.FromResult(new ShardHealthReport
        {
            ShardId = new ShardId("shard-1"),
            Status = ShardHealthStatus.Healthy,
            Timestamp = DateTimeOffset.UtcNow
        }));
        var options = new ShardHealthPolicyOptions
        {
            ProbeInterval = TimeSpan.Zero,
            UnhealthyThreshold = 2,
            HealthyThreshold = 2,
            ReactiveTrackingEnabled = true
        };
        var policy = new PeriodicShardHealthPolicy(probe, options);
        var shardId = new ShardId("shard-1");
        var exception = new InvalidOperationException("test failure");

        // act
        await policy.RecordFailureAsync(shardId, exception);
        await policy.RecordFailureAsync(shardId, exception);
        var unhealthyReport = await policy.GetHealthAsync(shardId);

        await policy.RecordSuccessAsync(shardId);
        await policy.RecordSuccessAsync(shardId);
        var healthyReport = await policy.GetHealthAsync(shardId);

        // assert
        unhealthyReport.Status.Should().Be(ShardHealthStatus.Unhealthy);
        healthyReport.Status.Should().Be(ShardHealthStatus.Healthy);
    }

    [Fact]
    public async Task ProbeAsync_Updates_Health_Status()
    {
        // arrange
        var probe = new TestProbe(shardId => ValueTask.FromResult(new ShardHealthReport
        {
            ShardId = shardId,
            Status = ShardHealthStatus.Healthy,
            Timestamp = DateTimeOffset.UtcNow,
            ProbeDurationMs = 42.0
        }));
        var options = new ShardHealthPolicyOptions { ProbeInterval = TimeSpan.Zero };
        var policy = new PeriodicShardHealthPolicy(probe, options);
        var shardId = new ShardId("shard-1");

        // act
        var probeReport = await policy.ProbeAsync(shardId);
        var statusReport = await policy.GetHealthAsync(shardId);

        // assert
        probeReport.Status.Should().Be(ShardHealthStatus.Healthy);
        statusReport.Status.Should().Be(ShardHealthStatus.Healthy);
        statusReport.ProbeDurationMs.Should().Be(42.0);
    }

    [Fact]
    public async Task GetAllHealthAsync_Returns_All_Tracked_Shards()
    {
        // arrange
        var probe = new TestProbe(_ => ValueTask.FromResult(new ShardHealthReport
        {
            ShardId = new ShardId("shard-1"),
            Status = ShardHealthStatus.Healthy,
            Timestamp = DateTimeOffset.UtcNow
        }));
        var options = new ShardHealthPolicyOptions
        {
            ProbeInterval = TimeSpan.Zero,
            ReactiveTrackingEnabled = true
        };
        var shardIds = new[] { new ShardId("shard-1"), new ShardId("shard-2"), new ShardId("shard-3") };
        var policy = new PeriodicShardHealthPolicy(probe, options, shardIds);

        // act
        var reports = new List<ShardHealthReport>();
        await foreach (var report in policy.GetAllHealthAsync())
        {
            reports.Add(report);
        }

        // assert
        reports.Should().HaveCount(3);
        reports.Select(r => r.ShardId).Should().BeEquivalentTo(shardIds);
    }

    [Fact]
    public void Dispose_Does_Not_Throw()
    {
        // arrange
        var probe = new TestProbe(_ => ValueTask.FromResult(new ShardHealthReport
        {
            ShardId = new ShardId("shard-1"),
            Status = ShardHealthStatus.Healthy,
            Timestamp = DateTimeOffset.UtcNow
        }));
        var policy = new PeriodicShardHealthPolicy(probe);

        // act/assert
        policy.Dispose();
    }
}
