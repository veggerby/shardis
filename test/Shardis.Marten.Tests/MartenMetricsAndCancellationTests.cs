using Marten;
using NSubstitute;
using Shardis.Marten;
using Shardis.Model;
using Shardis.Query.Diagnostics;
using Shardis.Query.Marten;
using Xunit;
using AwesomeAssertions;

namespace Shardis.Marten.Tests;

public sealed class MartenMetricsAndCancellationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fx;
    public MartenMetricsAndCancellationTests(PostgresContainerFixture fx) => _fx = fx;

    [PostgresFact]
    public async Task Metrics_Lifecycle_AdaptivePaging()
    {
        if (_fx.Store is null) return; // skipped
    var shard = new MartenShard(new ShardId("1"), _fx.Store);
        await Seed(shard, 200);
        var metrics = Substitute.For<IQueryMetricsObserver>();
        var exec = MartenQueryExecutor.Instance.WithMetrics(metrics).WithAdaptivePaging(minPageSize:32, maxPageSize:256, targetBatchMilliseconds:5);
        var session = shard.CreateSession();
        var seen = 0;
    await foreach (var _ in exec.Execute<Person>(session, q => q.Where(p => p.Age > 0).Select(p => p)))
        {
            seen++;
            if (seen >= 50) break; // partial enumeration
        }
    seen.Should().BeGreaterThan(0); // assert
        metrics.Received().OnShardStart(0);
        metrics.Received().OnItemsProduced(0, Arg.Is<int>(i => i > 0));
        metrics.Received().OnShardStop(0);
        metrics.Received().OnCompleted();
    }

    [PostgresFact]
    public async Task Cancellation_StopsEnumeration_ReportsCanceled()
    {
        if (_fx.Store is null) return;
    var shard = new MartenShard(new ShardId("2"), _fx.Store);
        await Seed(shard, 500);
        var metrics = Substitute.For<IQueryMetricsObserver>();
        var exec = MartenQueryExecutor.Instance.WithMetrics(metrics).WithPageSize(64);
        using var session = shard.CreateSession();
        using var cts = new CancellationTokenSource();
        var enumerated = 0;
    await foreach (var _ in exec.Execute<Person>(session, q => q.Where(p => p.Age > 0).Select(p => p)).WithCancellation(cts.Token))
        {
            enumerated++;
            if (enumerated == 30)
            {
                cts.Cancel();
            }
        }
    enumerated.Should().BeGreaterThan(0); // assert
        metrics.Received().OnShardStart(0);
        metrics.Received().OnItemsProduced(0, Arg.Is<int>(i => i > 0));
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

    private sealed class Person { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; public int Age { get; set; } }
}