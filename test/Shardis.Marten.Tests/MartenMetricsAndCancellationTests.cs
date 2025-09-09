using AwesomeAssertions;

using NSubstitute;

using Shardis.Model;
using Shardis.Query.Diagnostics;

using Xunit;

namespace Shardis.Marten.Tests;

public sealed class MartenMetricsAndCancellationTests(PostgresContainerFixture fx) : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fx = fx;

    [PostgresFact]
    public async Task Metrics_Lifecycle_AdaptivePaging()
    {
        // arrange
        if (_fx.Store is null) return; // skipped
        var shard = new MartenShard(new ShardId("1"), _fx.Store);
        await Seed(shard, 200);
        var metrics = Substitute.For<IQueryMetricsObserver>();
        var exec = MartenQueryExecutor.Instance
            .WithMetrics(metrics)
            .WithAdaptivePaging(minPageSize: 32, maxPageSize: 256, targetBatchMilliseconds: 5);
        var session = shard.CreateSession();

        // act
        var seen = 0;
        await foreach (var _ in exec.Execute<Person>(session, q => q.Where(p => p.Age > 0).Select(p => p)))
        {
            seen++;
            if (seen >= 50) break; // partial enumeration
        }

        // assert
        seen.Should().BeGreaterThan(0);
        metrics.Received().OnShardStart(0);
        // Provide explicit matcher for both int arguments to avoid NSubstitute AmbiguousArgumentsException
        metrics.ReceivedWithAnyArgs().OnItemsProduced(default, default);
        metrics.Received().OnShardStop(0);
        metrics.Received().OnCompleted();
    }

    [PostgresFact]
    public async Task Cancellation_StopsEnumeration_ReportsCanceled()
    {
        // arrange
        if (_fx.Store is null) return;
        var shard = new MartenShard(new ShardId("2"), _fx.Store);
        await Seed(shard, 500);
        var metrics = Substitute.For<IQueryMetricsObserver>();
        var exec = MartenQueryExecutor.Instance
            .WithMetrics(metrics)
            .WithPageSize(64);
        using var session = shard.CreateSession();
        using var cts = new CancellationTokenSource();

        // act
        var enumerated = 0;
        await foreach (var _ in exec.Execute<Person>(session, q => q.Where(p => p.Age > 0).Select(p => p)).WithCancellation(cts.Token))
        {
            enumerated++;
            if (enumerated == 30)
            {
                cts.Cancel();
            }
        }

        // assert
        enumerated.Should().BeGreaterThan(0);
        metrics.Received().OnShardStart(0);
        // Provide explicit matcher for both int arguments to avoid NSubstitute AmbiguousArgumentsException
        metrics.ReceivedWithAnyArgs().OnItemsProduced(default, default);
        metrics.Received().OnShardStop(0);
        metrics.Received().OnCanceled();
        metrics.DidNotReceive().OnCompleted();
    }

    private static async Task Seed(MartenShard shard, int count)
    {
        using var session = shard.CreateSession();
        for (var i = 0; i < count; i++)
        {
            session.Store(new Person { Id = Guid.NewGuid(), Name = "P" + i, Age = 20 + (i % 50) });
        }
        await session.SaveChangesAsync();
    }
}